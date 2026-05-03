using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;

namespace Quotations.Api.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _tokens;

    public RefreshTokenRepository(MongoDbService mongoDbService)
    {
        _tokens = mongoDbService.GetCollection<RefreshToken>("refreshTokens");
    }

    public async Task<RefreshToken> CreateAsync(RefreshToken token)
    {
        await _tokens.InsertOneAsync(token);
        return token;
    }

    public async Task<RefreshToken?> FindByTokenAsync(string token)
    {
        return await _tokens
            .Find(t => t.Token == token && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
    }

    public async Task RevokeAsync(string token)
    {
        var update = Builders<RefreshToken>.Update.Set(t => t.IsRevoked, true);
        await _tokens.UpdateOneAsync(t => t.Token == token, update);
    }

    public async Task RevokeAllForUserAsync(string userId)
    {
        var update = Builders<RefreshToken>.Update.Set(t => t.IsRevoked, true);
        await _tokens.UpdateManyAsync(t => t.UserId == userId && !t.IsRevoked, update);
    }
}
