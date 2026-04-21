using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quotations.Api.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("roles")]
    public List<string> Roles { get; set; } = new() { "User" };

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("submissionCount")]
    public int SubmissionCount { get; set; } = 0;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
