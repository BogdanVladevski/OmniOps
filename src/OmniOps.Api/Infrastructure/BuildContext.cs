namespace OmniOps.Api.Infrastructure;

internal static class BuildContext
{
    /// <summary>True while the build starts the app only to write the OpenAPI document (no database, no secrets).</summary>
    public static bool IsGeneratingOpenApiDocument { get; } =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
