using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Infrastructure.Auditing;
using OmniOps.Infrastructure.BackgroundJobs;
using OmniOps.Infrastructure.Email;
using OmniOps.Infrastructure.Identity;
using OmniOps.Infrastructure.Persistence;
using OmniOps.Infrastructure.Storage;

namespace OmniOps.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name of the PostgreSQL connection string (also the Aspire resource name).</summary>
    public const string DatabaseName = "omniops";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(
                    sp.GetRequiredService<IConfiguration>().GetConnectionString(DatabaseName)
                        ?? throw new InvalidOperationException($"Set the connection string 'ConnectionStrings:{DatabaseName}'."),
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                );
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IInvoiceNumbering>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        services.AddMemoryCache();
        services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
        services.AddScoped<IAccountSessions, AccountSessions>();
        services.AddScoped<IAuditLog, AuditLog>();

        services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
        services.Configure<PublicUrlOptions>(builder.Configuration.GetSection(PublicUrlOptions.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPublicLinks, PublicLinks>();

        services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStore>(sp =>
        {
            var root = sp.GetRequiredService<IOptions<StorageOptions>>().Value.RootPath;
            var contentRoot = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            return new LocalFileStore(Path.IsPathRooted(root) ? root : Path.Combine(contentRoot, root));
        });

        services.AddHostedService<InvoiceReminderService>();
        services.AddHostedService<WaitingAlertService>();
        return builder;
    }
}
