using MongoDB.Bson;
using MongoDB.Driver;

namespace Quotations.Api.Data;

public static class DataSeeder
{
    public static async Task SeedDataAsync(IMongoDatabase database)
    {
        var authorsCollection = database.GetCollection<BsonDocument>("authors");
        var sourcesCollection = database.GetCollection<BsonDocument>("sources");
        var quotationsCollection = database.GetCollection<BsonDocument>("quotations");

        // Check if data already exists
        var authorCount = await authorsCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        if (authorCount > 0)
        {
            Console.WriteLine("Database already seeded. Skipping...");
            return;
        }

        Console.WriteLine("Seeding database with sample data...");

        // Sample authors
        var author1Id = ObjectId.GenerateNewId();
        var author2Id = ObjectId.GenerateNewId();
        var author3Id = ObjectId.GenerateNewId();

        var authors = new[]
        {
            new BsonDocument
            {
                { "_id", author1Id },
                { "name", "Mahatma Gandhi" },
                { "lifespan", "1869-1948" },
                { "occupation", "Political Leader, Philosopher" },
                { "biography", "Indian lawyer and anti-colonial nationalist who led the campaign for India's independence." },
                { "quotationCount", 0 },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow }
            },
            new BsonDocument
            {
                { "_id", author2Id },
                { "name", "Albert Einstein" },
                { "lifespan", "1879-1955" },
                { "occupation", "Theoretical Physicist" },
                { "biography", "German-born theoretical physicist who developed the theory of relativity." },
                { "quotationCount", 0 },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow }
            },
            new BsonDocument
            {
                { "_id", author3Id },
                { "name", "Maya Angelou" },
                { "lifespan", "1928-2014" },
                { "occupation", "Poet, Author" },
                { "biography", "American memoirist, poet, and civil rights activist." },
                { "quotationCount", 0 },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow }
            }
        };

        await authorsCollection.InsertManyAsync(authors);

        // Sample sources
        var source1Id = ObjectId.GenerateNewId();
        var source2Id = ObjectId.GenerateNewId();
        var source3Id = ObjectId.GenerateNewId();

        var sources = new[]
        {
            new BsonDocument
            {
                { "_id", source1Id },
                { "title", "The Story of My Experiments with Truth" },
                { "type", "book" },
                { "year", 1927 },
                { "additionalInfo", "Autobiography of Mahatma Gandhi" },
                { "quotationCount", 0 },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow }
            },
            new BsonDocument
            {
                { "_id", source2Id },
                { "title", "The World As I See It" },
                { "type", "book" },
                { "year", 1949 },
                { "additionalInfo", "Collection of essays by Albert Einstein" },
                { "quotationCount", 0 },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow }
            },
            new BsonDocument
            {
                { "_id", source3Id },
                { "title", "I Know Why the Caged Bird Sings" },
                { "type", "book" },
                { "year", 1969 },
                { "additionalInfo", "Autobiography by Maya Angelou" },
                { "quotationCount", 0 },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow }
            }
        };

        await sourcesCollection.InsertManyAsync(sources);

        // Sample quotations
        var quotations = new[]
        {
            new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "text", "Be the change you wish to see in the world." },
                { "author", new BsonDocument { { "id", author1Id }, { "name", "Mahatma Gandhi" } } },
                { "source", new BsonDocument { { "id", source1Id }, { "title", "The Story of My Experiments with Truth" }, { "type", "book" } } },
                { "tags", new BsonArray { "inspiration", "change", "philosophy" } },
                { "status", "Approved" },
                { "submittedBy", BsonNull.Value },
                { "submittedAt", DateTime.UtcNow.AddDays(-30) },
                { "reviewedBy", BsonNull.Value },
                { "reviewedAt", BsonNull.Value },
                { "rejectionReason", BsonNull.Value },
                { "createdAt", DateTime.UtcNow.AddDays(-30) },
                { "updatedAt", DateTime.UtcNow.AddDays(-30) }
            },
            new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "text", "Imagination is more important than knowledge. Knowledge is limited. Imagination encircles the world." },
                { "author", new BsonDocument { { "id", author2Id }, { "name", "Albert Einstein" } } },
                { "source", new BsonDocument { { "id", source2Id }, { "title", "The World As I See It" }, { "type", "book" } } },
                { "tags", new BsonArray { "imagination", "knowledge", "science" } },
                { "status", "Approved" },
                { "submittedBy", BsonNull.Value },
                { "submittedAt", DateTime.UtcNow.AddDays(-25) },
                { "reviewedBy", BsonNull.Value },
                { "reviewedAt", BsonNull.Value },
                { "rejectionReason", BsonNull.Value },
                { "createdAt", DateTime.UtcNow.AddDays(-25) },
                { "updatedAt", DateTime.UtcNow.AddDays(-25) }
            },
            new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "text", "I've learned that people will forget what you said, people will forget what you did, but people will never forget how you made them feel." },
                { "author", new BsonDocument { { "id", author3Id }, { "name", "Maya Angelou" } } },
                { "source", new BsonDocument { { "id", source3Id }, { "title", "I Know Why the Caged Bird Sings" }, { "type", "book" } } },
                { "tags", new BsonArray { "emotion", "memory", "impact" } },
                { "status", "Approved" },
                { "submittedBy", BsonNull.Value },
                { "submittedAt", DateTime.UtcNow.AddDays(-20) },
                { "reviewedBy", BsonNull.Value },
                { "reviewedAt", BsonNull.Value },
                { "rejectionReason", BsonNull.Value },
                { "createdAt", DateTime.UtcNow.AddDays(-20) },
                { "updatedAt", DateTime.UtcNow.AddDays(-20) }
            }
        };

        await quotationsCollection.InsertManyAsync(quotations);

        Console.WriteLine($"Database seeded successfully with {authors.Length} authors, {sources.Length} sources, and {quotations.Length} quotations.");
    }
}
