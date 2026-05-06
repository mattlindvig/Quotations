namespace Quotations.Api.Configuration;

public class AiReviewOptions
{
    public int PollingIntervalSeconds { get; set; } = 1800;
    public int BatchSize { get; set; } = 10;
    public int ConcurrentRequests { get; set; } = 3;
    public int DelayBetweenRequestsMs { get; set; } = 500;
    public int MaxRetries { get; set; } = 3;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
    public int MaxTokens { get; set; } = 4096;
    public bool UseWebSearch { get; set; } = true;
}
