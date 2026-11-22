using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OmniOps.Application.Commands;
using OmniOps.Application.Commands.Handlers;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Tests.Handlers;

public class BillingHandlerTests
{
    private static readonly Guid Org = new("0a000000-0000-0000-0000-0000000000bb");
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly IInvoiceRepository _invoices = Substitute.For<IInvoiceRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IPublicLinks _links = Substitute.For<IPublicLinks>();
    private readonly FixedClock _clock = new(Now);

    public BillingHandlerTests()
    {
        _tenant.OrganizationId.Returns(Org);
        _links.Invoice(Arg.Any<string>()).Returns(c => $"https://app.example/i/{c.Arg<string>()}");
        _invoices.AllocateInvoiceSequenceAsync(Org, Arg.Any<CancellationToken>()).Returns(7);
    }

    private static BillingProfile Profile() => new()
    {
        OrganizationId = Org, LegalName = "Vardar Transport DOOEL", Address = "Partizanska 1, Skopje",
        Iban = "MK07250120000058984", Currency = "EUR", DefaultPaymentTermsDays = 30, InvoicePrefix = "VT",
    };

    private static Trip Delivered() => new()
    {
        Id = Guid.NewGuid(), OrganizationId = Org, Status = TripStatus.Completed, CustomerName = "Tinex Kumanovo",
        CompletedAtUtc = Now.AddHours(-2), Vehicle = new Vehicle { ExternalId = "Truck-002" },
    };

    private CreateInvoiceCommandHandler CreateHandler() => new(_invoices, _jobs, _tenant, _audit, _clock);

    private static CreateInvoiceCommand Create(Guid jobId, string? email = "ap@tinex.mk", int? terms = null) =>
        new(jobId, "Tinex Kumanovo", email, "Kumanovo", null, terms, [new InvoiceLineInput("Transport", 1, 250m, 18m)], null);

    [Fact]
    public async Task Create_WithoutCompanyDetails_IsRejectedBeforeANumberIsTaken()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(Create(Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("Invoice settings", ex.Message);
        await _invoices.DidNotReceive().AllocateInvoiceSequenceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ForAJobNotYetDelivered_IsRejected()
    {
        _invoices.GetProfileAsync(Org, Arg.Any<CancellationToken>()).Returns(Profile());
        var job = Delivered();
        job.Status = TripStatus.InProgress;
        _jobs.GetAsync(job.Id, Org, Arg.Any<CancellationToken>()).Returns(job);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(Create(job.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Create_TwiceForTheSameDelivery_IsRejected()
    {
        _invoices.GetProfileAsync(Org, Arg.Any<CancellationToken>()).Returns(Profile());
        var job = Delivered();
        job.InvoiceId = Guid.NewGuid();
        _jobs.GetAsync(job.Id, Org, Arg.Any<CancellationToken>()).Returns(job);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(Create(job.Id), CancellationToken.None));
        await _invoices.DidNotReceive().AllocateInvoiceSequenceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_SnapshotsBothPartiesNumbersAndLinksTheJob()
    {
        _invoices.GetProfileAsync(Org, Arg.Any<CancellationToken>()).Returns(Profile());
        var job = Delivered();
        _jobs.GetAsync(job.Id, Org, Arg.Any<CancellationToken>()).Returns(job);
        Invoice? saved = null;
        await _invoices.AddAsync(Arg.Do<Invoice>(i => saved = i), Arg.Any<CancellationToken>());

        var dto = await CreateHandler().Handle(Create(job.Id, terms: 15), CancellationToken.None);

        Assert.Equal("VT-2026-0007", dto.Summary.Number);
        Assert.Equal(295.00m, dto.Summary.Total);
        Assert.Equal(Now.Date.AddDays(15), dto.Summary.DueDateUtc);
        Assert.Equal("Issued", dto.Summary.Status);
        Assert.Equal("Vardar Transport DOOEL", saved!.SellerName);
        Assert.Equal("MK07250120000058984", saved.SellerIban);
        Assert.Equal(saved.Id, job.InvoiceId);
        await _invoices.Received(1).AddCustomerAsync(
            Arg.Is<Customer>(c => c.NormalizedName == "tinex kumanovo" && c.Email == "ap@tinex.mk" && c.PaymentTermsDays == 15),
            Arg.Any<CancellationToken>());
    }

    private SendInvoiceCommandHandler SendHandler() =>
        new(_invoices, _tenant, _email, _links, _audit, _clock, NullLogger<SendInvoiceCommandHandler>.Instance);

    private Invoice Issued(string? email = "ap@tinex.mk")
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), OrganizationId = Org, Number = "VT-2026-0007", Status = InvoiceStatus.Issued, CustomerName = "Tinex",
            CustomerEmail = email, Total = 295m, Currency = "EUR", DueDateUtc = Now.AddDays(30), PublicToken = "tok_abcdefghijklmnop",
            SellerName = "Vardar Transport", SellerIban = "MK07250120000058984",
        };
        _invoices.GetAsync(invoice.Id, Org, Arg.Any<CancellationToken>()).Returns(invoice);
        return invoice;
    }

    [Fact]
    public async Task Send_ByEmail_EmailsTheLinkThenMarksItSent()
    {
        _email.IsConfigured.Returns(true);
        var invoice = Issued();

        var dto = await SendHandler().Handle(new SendInvoiceCommand(invoice.Id, Email: true), CancellationToken.None);

        await _email.Received(1).SendAsync("ap@tinex.mk", Arg.Is<string>(s => s.Contains("VT-2026-0007")),
            Arg.Is<string>(b => b.Contains("https://app.example/i/tok_abcdefghijklmnop") && b.Contains("MK07250120000058984")),
            Arg.Any<CancellationToken>());
        Assert.True(dto.Emailed);
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        Assert.Equal(Now, invoice.SentAtUtc);
    }

    [Fact]
    public async Task Send_ByEmail_WhenTheEmailFails_LeavesTheInvoiceUnsent()
    {
        _email.IsConfigured.Returns(true);
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("smtp down"));
        var invoice = Issued();

        await Assert.ThrowsAsync<BusinessRuleException>(() => SendHandler().Handle(new SendInvoiceCommand(invoice.Id, true), CancellationToken.None));

        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Null(invoice.SentAtUtc);
    }

    [Fact]
    public async Task Send_ByEmail_WithoutAMailServer_SaysSoInsteadOfPretending()
    {
        _email.IsConfigured.Returns(false);
        var invoice = Issued();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => SendHandler().Handle(new SendInvoiceCommand(invoice.Id, true), CancellationToken.None));

        Assert.Contains("Copy the invoice link", ex.Message);
    }

    [Fact]
    public async Task Send_Manually_JustRecordsIt()
    {
        var invoice = Issued(email: null);

        var dto = await SendHandler().Handle(new SendInvoiceCommand(invoice.Id, Email: false), CancellationToken.None);

        Assert.Equal("Sent", dto.Summary.Status);
        Assert.False(dto.Emailed);
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkPaid_StopsRemindersAndKeepsTheFirstPaymentDate()
    {
        var invoice = Issued();
        invoice.Status = InvoiceStatus.Sent;
        var handler = new MarkInvoicePaidCommandHandler(_invoices, _tenant, _audit, _clock);

        await handler.Handle(new MarkInvoicePaidCommand(invoice.Id, new DateTimeOffset(Now.AddDays(-1))), CancellationToken.None);
        await handler.Handle(new MarkInvoicePaidCommand(invoice.Id, null), CancellationToken.None);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(Now.AddDays(-1), invoice.PaidAtUtc);
        Assert.Null(Core.Domain.InvoiceRules.DueReminder(invoice, Now.AddDays(60)));
    }

    private ProcessInvoiceRemindersCommandHandler ReminderHandler() =>
        new(_invoices, _email, _links, _clock, NullLogger<ProcessInvoiceRemindersCommandHandler>.Instance);

    private Invoice OverdueSent(string? email)
    {
        var invoice = Issued(email);
        invoice.Status = InvoiceStatus.Sent;
        invoice.SentAtUtc = Now.AddDays(-40);
        invoice.DueDateUtc = Now.Date.AddDays(-8);
        return invoice;
    }

    [Fact]
    public async Task Reminders_AreEmailedAndRecordedOnce()
    {
        _email.IsConfigured.Returns(true);
        var invoice = OverdueSent("ap@tinex.mk");
        _invoices.ListAwaitingPaymentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([invoice]);

        var first = await ReminderHandler().Handle(new ProcessInvoiceRemindersCommand(), CancellationToken.None);
        var second = await ReminderHandler().Handle(new ProcessInvoiceRemindersCommand(), CancellationToken.None);

        Assert.Equal(1, first.Emailed);
        Assert.Equal(0, second.Sent);
        await _email.Received(1).SendAsync("ap@tinex.mk", Arg.Is<string>(s => s.StartsWith("Overdue")),
            Arg.Is<string>(b => b.Contains("already paid")), Arg.Any<CancellationToken>());
        Assert.Single(invoice.Reminders, r => r.Kind == ReminderKind.Overdue7 && r.Emailed);
    }

    [Fact]
    public async Task Reminders_WithoutAnEmailAddress_AreRecordedForTheOffice()
    {
        _email.IsConfigured.Returns(true);
        var invoice = OverdueSent(email: null);
        _invoices.ListAwaitingPaymentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([invoice]);

        var run = await ReminderHandler().Handle(new ProcessInvoiceRemindersCommand(), CancellationToken.None);

        Assert.Equal(1, run.Sent);
        Assert.Equal(0, run.Emailed);
        Assert.Single(invoice.Reminders, r => !r.Emailed);
    }

    [Fact]
    public async Task Reminders_WhenTheEmailFails_AreRetriedNextRun()
    {
        _email.IsConfigured.Returns(true);
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("smtp down"));
        var invoice = OverdueSent("ap@tinex.mk");
        _invoices.ListAwaitingPaymentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([invoice]);

        var run = await ReminderHandler().Handle(new ProcessInvoiceRemindersCommand(), CancellationToken.None);

        Assert.Equal(0, run.Sent);
        Assert.Empty(invoice.Reminders);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
