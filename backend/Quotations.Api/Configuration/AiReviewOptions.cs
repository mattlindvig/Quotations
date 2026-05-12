namespace Quotations.Api.Configuration;

public class AiReviewOptions
{
    public int PollingIntervalSeconds { get; set; } = 1800;
    public int BatchSize { get; set; } = 10;
    public int ConcurrentRequests { get; set; } = 3;
    public int DelayBetweenRequestsMs { get; set; } = 500;
    public int MaxRetries { get; set; } = 3;

    // Model used for standard AI review passes
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    // Model used when a low-confidence result triggers a deeper review pass.
    // Set DeepReviewEnabled = false to skip the second pass entirely (saves ~30-40% cost).
    public string DeepReviewModel { get; set; } = "claude-haiku-4-5-20251001";
    public bool DeepReviewEnabled { get; set; } = false;

    // Model used by the chat service
    public string ChatModel { get; set; } = "claude-haiku-4-5-20251001";

    public int MaxTokens { get; set; } = 4096;

    // Web search adds significant cost per quote (~2x). Disable for bulk processing;
    // enable selectively for user-submitted quotes where accuracy matters more.
    public bool UseWebSearch { get; set; } = false;
}
