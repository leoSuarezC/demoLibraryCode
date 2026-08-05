namespace Library.Infrastructure.Ai;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Free-tier key from Google AI Studio. Left empty in source control; supplied
    /// through user secrets locally and an environment variable in deployment.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.0-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";

    /// <summary>Ceiling on a single enrichment call; a librarian will not wait longer.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
