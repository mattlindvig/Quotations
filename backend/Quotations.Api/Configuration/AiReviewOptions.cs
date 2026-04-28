namespace Quotations.Api.Configuration;

public class AiReviewOptions
{
    public int PollingIntervalSeconds { get; set; } = 1800;
    public int BatchSize { get; set; } = 10;
    public int ConcurrentRequests { get; set; } = 3;
    public int DelayBetweenRequestsMs { get; set; } = 500;
    public int MaxRetries { get; set; } = 3;
}
