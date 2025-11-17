using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Quotations.Api.Services;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}

public class MongoDbService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(IOptions<MongoDbSettings> settings, ILogger<MongoDbService> logger)
    {
        _logger = logger;

        _logger.LogInformation("Connecting to MongoDB at {ConnectionString}, Database: {DatabaseName}",
            MaskConnectionString(settings.Value.ConnectionString), settings.Value.DatabaseName);

        try
        {
            var mongoClient = new MongoClient(settings.Value.ConnectionString);
            _database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            _logger.LogInformation("Successfully connected to MongoDB database: {DatabaseName}", settings.Value.DatabaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MongoDB at {ConnectionString}",
                MaskConnectionString(settings.Value.ConnectionString));
            throw;
        }
    }

    public IMongoDatabase Database => _database;

    public IMongoDatabase GetDatabase() => _database;

    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        _logger.LogDebug("Getting collection: {CollectionName}", collectionName);
        return _database.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Masks sensitive information in connection string for logging
    /// </summary>
    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        // Mask password in connection string
        var uri = connectionString.Contains("://") ? new System.Uri(connectionString) : null;
        if (uri != null && !string.IsNullOrEmpty(uri.UserInfo))
        {
            return connectionString.Replace(uri.UserInfo, "***:***");
        }

        return connectionString;
    }
}
