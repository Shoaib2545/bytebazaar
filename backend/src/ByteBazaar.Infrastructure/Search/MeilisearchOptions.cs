namespace ByteBazaar.Infrastructure.Search;

/// <summary>Bound from the "Meilisearch" configuration section.</summary>
public class MeilisearchOptions
{
    public const string SectionName = "Meilisearch";

    /// <summary>Base URL, e.g. http://localhost:7701. Blank disables search indexing entirely.</summary>
    public string? Url { get; set; }

    /// <summary>Master/API key. Optional in development.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Index uid holding product documents.</summary>
    public string IndexName { get; set; } = "products";

    /// <summary>How long to stop calling Meilisearch after a failure, so an outage costs one timeout.</summary>
    public int CircuitBreakSeconds { get; set; } = 30;

    /// <summary>Per-request timeout; the storefront's suggest box must not hang on a dead engine.</summary>
    public int TimeoutSeconds { get; set; } = 3;
}
