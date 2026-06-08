using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quotations.Api.Models;

public enum WikiquoteSyncType { Full, Delta }
public enum WikiquoteSyncStatus { Running, Completed, Failed }

public class WikiquoteSyncRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public WikiquoteSyncType SyncType { get; set; }

    [BsonRepresentation(BsonType.String)]
    public WikiquoteSyncStatus Status { get; set; } = WikiquoteSyncStatus.Running;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public int PagesProcessed { get; set; }
    public int QuotesInserted { get; set; }
    public int QuotesSkipped { get; set; }

    // For delta syncs — the Wikiquote timestamp we last synced up to
    public DateTime? DeltaFromTimestamp { get; set; }

    public string? ErrorMessage { get; set; }
}
