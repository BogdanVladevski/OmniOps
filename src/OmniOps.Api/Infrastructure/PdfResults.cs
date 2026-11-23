using Microsoft.Net.Http.Headers;
using OmniOps.Application.Features.Jobs;

namespace OmniOps.Api.Infrastructure;

internal static class PdfResults
{
    /// <summary>Opens in the browser's PDF viewer (inline) and still saves under a sensible name.</summary>
    public static IResult Inline(HttpContext context, PdfFile pdf)
    {
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(pdf.FileName);
        context.Response.Headers.ContentDisposition = disposition.ToString();
        return TypedResults.File(pdf.Content, "application/pdf");
    }
}
