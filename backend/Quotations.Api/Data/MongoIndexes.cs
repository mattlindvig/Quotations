using MongoDB.Driver;

namespace Quotations.Api.Data;

public static class MongoIndexes
{
    public static async Task CreateIndexesAsync(IMongoDatabase database)
    {
        var quotationsCollection = database.GetCollection<object>("quotations");
        var authorsCollection = database.GetCollection<object>("authors");
        var sourcesCollection = database.GetCollection<object>("sources");

        // Quotations indexes — each index targets a specific query pattern.
        // MongoDB can use a compound index as a prefix, so (status, submittedAt) also
        // covers plain status-only queries.
        var quotationIndexes = new[]
        {
            // Full-text search across quote text, author name, and source title.
            // DefaultLanguage "none" disables stopword stripping so short common words
            // ("not", "no", "or", "is") are indexed and searchable — important for quotes.
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Text("text")
                    .Text("author.name")
                    .Text("source.title"),
                new CreateIndexOptions { Name = "text_search_idx", DefaultLanguage = "none" }
            ),

            // Default browse: filter by status, sort newest-first
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Descending("submittedAt"),
                new CreateIndexOptions { Name = "status_date_idx" }
            ),

            // Author filter + default sort (also covers status-only queries via prefix)
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Ascending("author.name")
                    .Descending("submittedAt"),
                new CreateIndexOptions { Name = "status_author_date_idx" }
            ),

            // Source type filter + default sort
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Ascending("source.type")
                    .Descending("submittedAt"),
                new CreateIndexOptions { Name = "status_sourcetype_date_idx" }
            ),

            // Source title filter
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Ascending("source.title"),
                new CreateIndexOptions { Name = "status_sourcetitle_idx" }
            ),

            // Tag filter + default sort (multikey index — one entry per tag value)
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("status")
                    .Ascending("tags")
                    .Descending("submittedAt"),
                new CreateIndexOptions { Name = "status_tags_date_idx" }
            ),

            // AI review queue — background service picks up NotReviewed/Pending, oldest first
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("aiReview.status")
                    .Ascending("submittedAt"),
                new CreateIndexOptions { Name = "ai_review_queue_idx" }
            ),

            // User's own submissions
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys
                    .Ascending("submittedBy.id")
                    .Descending("submittedAt"),
                new CreateIndexOptions { Name = "submitter_date_idx" }
            ),

            // Deduplication via SHA-256 hash of normalised text — much smaller than a
            // full-text unique index while still preventing duplicate quotes.
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("textHash"),
                new CreateIndexOptions { Name = "text_hash_unique_idx", Unique = true }
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

        // Users indexes
        var usersCollection = database.GetCollection<object>("users");
        var userIndexes = new[]
        {
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("username"),
                new CreateIndexOptions { Name = "username_idx", Unique = true }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("email"),
                new CreateIndexOptions { Name = "email_idx", Unique = true }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("emailVerificationToken"),
                new CreateIndexOptions { Name = "email_verification_token_idx", Sparse = true }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("passwordResetToken"),
                new CreateIndexOptions { Name = "password_reset_token_idx", Sparse = true }
            )
        };

        await usersCollection.Indexes.CreateManyAsync(userIndexes);

        // Quote of the Day — unique per date so concurrent instances can't double-insert
        var qotdCollection = database.GetCollection<object>("quoteOfDay");
        await qotdCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("date"),
                new CreateIndexOptions { Name = "date_idx", Unique = true }
            )
        );

        // Refresh tokens — fast lookup by hash, TTL auto-deletes expired tokens
        var refreshTokensCollection = database.GetCollection<object>("refreshTokens");
        await refreshTokensCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("token"),
                new CreateIndexOptions { Name = "token_idx", Unique = true }
            ),
            new CreateIndexModel<object>(
                Builders<object>.IndexKeys.Ascending("expiresAt"),
                new CreateIndexOptions { Name = "token_ttl_idx", ExpireAfter = TimeSpan.Zero }
            )
        });
    }
}
