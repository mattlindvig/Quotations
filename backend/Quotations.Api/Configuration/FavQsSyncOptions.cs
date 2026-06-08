namespace Quotations.Api.Configuration;

public class FavQsSyncOptions
{
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public int DelayBetweenRequestsMs { get; set; } = 600;
    public int MinQuoteLength { get; set; } = 20;
    // Re-sync FavQs this many days after the last completed run to pick up new quotes
    public int ReSyncIntervalDays { get; set; } = 30;
}
