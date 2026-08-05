using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Infrastructure.Ai;

/// <summary>
/// Asks a language model to draft the descriptive fields a librarian would otherwise
/// type: synopsis, category, publication year and tags.
/// </summary>
/// <remarks>
/// Three things keep this from being a liability:
///
/// 1. The model is constrained to JSON matching a schema, so the response is parsed
///    rather than interpreted. No scraping prose for a year.
/// 2. Failure is not fatal. A model that is slow, rate-limited or plain wrong must not
///    stop a librarian cataloguing a book, so errors degrade to "no suggestion".
/// 3. Nothing is written to the catalogue. The suggestion populates a form; a human
///    confirms it. A model is confidently wrong often enough that this matters.
/// </remarks>
public class GeminiMetadataService(
    HttpClient http,
    IOptions<GeminiOptions> options,
    ILogger<GeminiMetadataService> logger) : IAiMetadataService
{
    private readonly GeminiOptions _options = options.Value;

    private const string SystemPrompt = """
        You are a reference librarian cataloguing a book.

        Given a title and an author, return the catalogue metadata you are confident
        about. Follow these rules strictly:

        - synopsis: two or three sentences, neutral and factual. No spoilers, no
          marketing language, no opinions.
        - category: a single conventional library subject, such as "Science Fiction",
          "History", "Software" or "Biography".
        - publishedYear: the year of first publication, not of a later edition. Use
          null if you are not confident.
        - tags: three short lowercase keywords a reader might search for.

        If you do not recognise the work, say so by returning null for synopsis and
        publishedYear rather than inventing details. An empty field is far more useful
        to a librarian than a plausible fabrication.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsConfigured => _options.IsConfigured;

    public async Task<AiMetadataSuggestion?> SuggestAsync(
        string title,
        string author,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Metadata enrichment skipped: no Gemini API key configured.");
            return null;
        }

        var request = BuildRequest(title, author);
        var url = $"v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        try
        {
            using var response = await http.PostAsJsonAsync(url, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Retries have already been exhausted by the resilience handler, so
                // this is a considered failure rather than a blip.
                logger.LogWarning(
                    "Gemini returned {StatusCode} enriching '{Title}'.",
                    (int)response.StatusCode, title);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, ct);

            var json = payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(json))
            {
                logger.LogWarning("Gemini returned an empty candidate for '{Title}'.", title);
                return null;
            }

            var suggestion = JsonSerializer.Deserialize<GeminiSuggestion>(json, JsonOptions);
            if (suggestion is null) return null;

            return new AiMetadataSuggestion(
                Synopsis: Blank(suggestion.Synopsis),
                Category: Blank(suggestion.Category),
                PublishedYear: Plausible(suggestion.PublishedYear),
                Tags: suggestion.Tags?
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .Select(t => t.Trim().ToLowerInvariant())
                          .Distinct()
                          .Take(5)
                          .ToArray()
                      ?? []);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;   // the caller went away; not our failure to swallow
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Metadata enrichment failed for '{Title}'.", title);
            return null;
        }
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Drops years the model cannot plausibly be right about. The domain would reject
    /// them anyway; catching it here means the librarian sees a blank field rather
    /// than a validation error on a value they did not type.
    /// </summary>
    private static int? Plausible(int? year) =>
        year is >= 1450 && year <= 2100 ? year : null;

    private object BuildRequest(string title, string author) => new
    {
        system_instruction = new { parts = new[] { new { text = SystemPrompt } } },
        contents = new[]
        {
            new { parts = new[] { new { text = $"Title: {title}\nAuthor: {author}" } } }
        },
        generationConfig = new
        {
            // Asking for JSON is not enough on its own - a schema is what makes the
            // response safe to deserialise straight into a DTO.
            response_mime_type = "application/json",
            response_schema = new
            {
                type = "OBJECT",
                properties = new
                {
                    synopsis = new { type = "STRING", nullable = true },
                    category = new { type = "STRING", nullable = true },
                    publishedYear = new { type = "INTEGER", nullable = true },
                    tags = new { type = "ARRAY", items = new { type = "STRING" } }
                },
                required = new[] { "tags" }
            },
            temperature = 0.2   // cataloguing wants consistency, not flair
        }
    };

    // ---- Wire format ------------------------------------------------------

    private sealed record GeminiSuggestion(
        string? Synopsis,
        string? Category,
        [property: JsonPropertyName("publishedYear")] int? PublishedYear,
        string[]? Tags);

    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")] GeminiCandidate[]? Candidates);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] GeminiPart[]? Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string? Text);
}
