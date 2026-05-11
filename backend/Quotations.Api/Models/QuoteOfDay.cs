using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quotations.Api.Models;

public class QuoteOfDay
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("date")]
    public string Date { get; set; } = string.Empty; // "yyyy-MM-dd" UTC

    [BsonElement("quotationId")]
    public string QuotationId { get; set; } = string.Empty;
}
