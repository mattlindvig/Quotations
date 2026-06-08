using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quotations.Api.Models;

public enum FavQsSyncStatus { Running, Completed, Failed }

public class FavQsSyncRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public FavQsSyncStatus Status { get; set; } = FavQsSyncStatus.Running;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public int PagesProcessed { get; set; }
    public int QuotesInserted { get; set; }
    public int QuotesSkipped { get; set; }

    // Last successfully processed page — allows resuming a cancelled sync
    public int? ResumePage { get; set; }

    public string? ErrorMessage { get; set; }
}
