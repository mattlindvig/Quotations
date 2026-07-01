namespace Quotations.Api.Configuration;

/// <summary>
/// Voyage AI embeddings configuration. Voyage is the embeddings provider Anthropic
/// documents (Anthropic has no first-party embeddings API). Used to embed the user's
/// search query for Meilisearch hybrid (semantic) search.
/// </summary>
public class VoyageSettings
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "voyage-3.5-lite";
    public int Dimensions { get; set; } = 1024;

    /// <summary>Semantic weight for hybrid search (0 = pure keyword, 1 = pure vector).</summary>
    public double SemanticRatio { get; set; } = 0.7;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
