namespace OmniOps.Infrastructure.Configuration;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Directory for uploaded files; relative paths resolve against the app's content root.</summary>
    public string RootPath { get; set; } = "data/uploads";
}
