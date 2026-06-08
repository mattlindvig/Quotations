namespace Quotations.Api.Configuration;

public class WikiquoteSyncOptions
{
    public bool Enabled { get; set; } = true;

    // How many days between delta syncs
    public int DeltaIntervalDays { get; set; } = 30;

    // Delay between Wikiquote API requests (be a good citizen)
    public int DelayBetweenRequestsMs { get; set; } = 1000;

    // Max pages to process per run (0 = unlimited)
    public int MaxPagesPerRun { get; set; } = 0;

    // Minimum quote length to bother importing
    public int MinQuoteLength { get; set; } = 30;
}
