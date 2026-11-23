using System.Globalization;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Features.Jobs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OmniOps.Infrastructure.Documents;

/// <summary>
/// The proof pack as an A4 PDF: a summary, the timeline, waiting time, the temperature log, then every photo with when
/// and where it was taken. Quiet layout in the app's style: greys, hairlines, colour only for a problem.
/// </summary>
internal sealed class QuestPdfProofPackRenderer : IProofPackRenderer
{
    private const string Ink = "#16181D";
    private const string Secondary = "#545A66";
    private const string Muted = "#8A8F99";
    private const string Hairline = "#E4E4E0";
    private const string Critical = "#C8322B";
    private const string Warning = "#A85C0A";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    static QuestPdfProofPackRenderer()
    {
        // Community licence: free for companies under US$1M annual revenue. Review before that threshold.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ProofPack pack) => Document.Create(document => document.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink));

        page.Header().Element(header => Header(header, pack));
        page.Content().PaddingTop(14).Column(column =>
        {
            column.Spacing(16);
            column.Item().Element(c => Summary(c, pack));
            column.Item().Element(c => Timeline(c, pack));
            if (pack.Waits.Count > 0)
            {
                column.Item().Element(c => Waits(c, pack));
            }

            if (pack.TemperatureRange is not null)
            {
                column.Item().Element(c => Temperatures(c, pack));
            }

            if (pack.Images.Count > 0)
            {
                column.Item().Element(c => Images(c, pack));
            }
        });
        page.Footer().Row(row =>
        {
            row.RelativeItem().Text($"Generated {Time(pack.GeneratedAt)} ({pack.TimeZone}) by {pack.CompanyName}").FontSize(8).FontColor(Muted);
            row.ConstantItem(80).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    })).GeneratePdf();

    private static void Header(IContainer container, ProofPack pack) => container.BorderBottom(0.5f).BorderColor(Hairline).PaddingBottom(10).Row(row =>
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(pack.CompanyName).FontSize(14).Bold();
            if (pack.CompanyDetails is not null)
            {
                c.Item().Text(pack.CompanyDetails).FontSize(8).FontColor(Secondary);
            }
        });
        row.ConstantItem(170).AlignRight().Column(c =>
        {
            c.Item().AlignRight().Text("Proof of delivery").FontSize(14).Bold();
            c.Item().AlignRight().Text(pack.Reference is null ? pack.CustomerName : $"Order {pack.Reference}").FontColor(Secondary);
        });
    });

    private static void Summary(IContainer container, ProofPack pack)
    {
        var rows = new List<(string, string?)>
        {
            ("Customer", pack.CustomerName),
            ("Status", pack.Status),
            ("From", pack.Origin),
            ("Delivered to", pack.Destination),
            ("Received by", pack.ReceivedBy),
            ("Truck", pack.Truck),
            ("Keep at", pack.TemperatureRange),
            ("Note at delivery", pack.DeliveryNote),
        };
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(110);
                c.RelativeColumn();
            });
            foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Item2)))
            {
                table.Cell().Element(Cell).Text(label).FontColor(Secondary);
                table.Cell().Element(Cell).Text(value!).SemiBold();
            }
        });
    }

    private static void Timeline(IContainer container, ProofPack pack) => Section(container, "Timeline", c => c.Table(table =>
    {
        table.ColumnsDefinition(cols =>
        {
            cols.RelativeColumn();
            cols.ConstantColumn(130);
        });
        foreach (var step in pack.Timeline)
        {
            table.Cell().Element(Cell).Text(step.Label);
            table.Cell().Element(Cell).AlignRight().Text(Time(step.At)).FontColor(Secondary);
        }
    }));

    private static void Waits(IContainer container, ProofPack pack) => Section(container, "Waiting time", c => c.Column(column =>
    {
        foreach (var wait in pack.Waits)
        {
            column.Item().Element(Cell).Column(w =>
            {
                w.Item().Row(r =>
                {
                    r.RelativeItem().Text(wait.Stop).SemiBold();
                    r.ConstantItem(140).AlignRight().Text(Duration(wait.WaitedMinutes) + (wait.ChargedMinutes > 0 ? $" ({Duration(wait.ChargedMinutes)} chargeable)" : string.Empty))
                        .SemiBold().FontColor(wait.ChargedMinutes > 0 ? Warning : Ink);
                });
                w.Item().Text($"Arrived {Time(wait.ArrivedAt)}" + (wait.ReleasedAt is { } r2 ? $" · released {Time(r2)}" : " · still waiting")).FontColor(Secondary);
                w.Item().Text(wait.Evidence).FontSize(8).FontColor(Muted);
            });
        }
    }));

    private static void Temperatures(IContainer container, ProofPack pack) => Section(container, $"Temperature log ({pack.TemperatureRange})", c =>
    {
        if (pack.Readings.Count == 0)
        {
            c.Text("No readings were logged.").FontColor(Secondary);
            return;
        }

        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.ConstantColumn(90);
            });
            foreach (var reading in pack.Readings)
            {
                table.Cell().Element(Cell).Text(Time(reading.At)).FontColor(Secondary);
                table.Cell().Element(Cell).AlignRight().Text(t =>
                {
                    var value = t.Span(string.Create(Culture, $"{reading.Celsius:0.0} °C")).SemiBold();
                    if (reading.OutOfRange)
                    {
                        value.FontColor(Critical);
                        t.Span("  outside range").FontSize(8).FontColor(Critical);
                    }
                });
            }
        });
    });

    private static void Images(IContainer container, ProofPack pack) => Section(container, "Photos and signatures", c => c.Table(table =>
    {
        table.ColumnsDefinition(cols =>
        {
            cols.RelativeColumn();
            cols.RelativeColumn();
        });
        foreach (var image in pack.Images)
        {
            var decoded = Decode(image.Content);
            table.Cell().Padding(4).ShowEntire().Column(col =>
            {
                var frame = col.Item().Border(0.5f).BorderColor(Hairline).Height(190).AlignCenter().AlignMiddle();
                if (decoded is null)
                {
                    // A damaged upload must never stop the rest of the evidence from being produced.
                    frame.Padding(12).Text("This file could not be included. The original is kept on record.").FontSize(8).FontColor(Muted);
                }
                else
                {
                    frame.Image(decoded).FitArea();
                }

                col.Item().PaddingTop(4).Text(image.Title).SemiBold();
                col.Item().Text(image.Caption).FontSize(8).FontColor(Secondary);
            });
        }
    }));

    /// <summary>Uploads come from phones: anything that is not a readable image is reported in the PDF, not thrown.</summary>
    private static Image? Decode(byte[] content)
    {
        try
        {
            return Image.FromBinaryData(content);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static void Section(IContainer container, string title, Action<IContainer> body) => container.Column(c =>
    {
        c.Item().PaddingBottom(4).Text(title).FontSize(11).Bold();
        body(c.Item());
    });

    private static IContainer Cell(IContainer container) => container.BorderBottom(0.5f).BorderColor(Hairline).PaddingVertical(5);

    private static string Time(DateTime local) => local.ToString("d MMM yyyy, HH:mm", Culture);

    private static string Duration(int minutes) => minutes < 60
        ? $"{minutes} min"
        : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60:00} min";
}
