using MongoDB.Driver;

namespace Quotations.Api.Data;

public static class MongoIndexes
{
    public static async Task CreateIndexesAsync(IMongoDatabase database)
    {
        var quotationsCollection = database.GetCollection<object>("quotations");
        var authorsCollection = database.GetCollection<object>("authors");
        var sourcesCollection = database.GetCollection<object>("sources");

        // Quotations indexes
        var quotationIndexes = new[]
        {
            // Text search index
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Text("text")
                    .Text("author.name")
                    .Text("source.title"),
                new CreateIndexOptions { Name = "text_search_idx" }
            ),
            // Filter and sort index
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Ascending("author.id")
                    .Ascending("source.type")
                    .Descending("submittedAt"),
                new CreateIndexOptions { Name = "filter_sort_idx" }
            ),
            // Tag filtering
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("tags"),
                new CreateIndexOptions { Name = "tags_idx" }
            ),
            // Review queue
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Ascending("submittedAt"),
                new CreateIndexOptions { Name = "review_queue_idx" }
            )
        };

        await quotationsCollection.Indexes.CreateManyAsync(quotationIndexes);

        // Authors indexes
        var authorIndexes = new[]
        {
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("name"),
                new CreateIndexOptions { Name = "author_name_idx", Unique = true }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Text("name"),
                new CreateIndexOptions { Name = "author_text_idx" }
            )
        };

        await authorsCollection.Indexes.CreateManyAsync(authorIndexes);

        // Sources indexes
        var sourceIndexes = new[]
        {
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("title")
                    .Ascending("type"),
                new CreateIndexOptions { Name = "source_title_type_idx", Unique = true }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Text("title"),
                new CreateIndexOptions { Name = "source_text_idx" }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("type"),
                new CreateIndexOptions { Name = "source_type_idx" }
            )
        };

        await sourcesCollection.Indexes.CreateManyAsync(sourceIndexes);
    }
}
