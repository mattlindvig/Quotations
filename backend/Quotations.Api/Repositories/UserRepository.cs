using MongoDB.Bson;
using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;

namespace Quotations.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(MongoDbService mongoDbService)
    {
        _users = mongoDbService.GetCollection<User>("users");
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;

        return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var normalised = username.Trim().ToLowerInvariant();
        return await _users
            .Find(u => u.Username.ToLower() == normalised)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();
        return await _users
            .Find(u => u.Email == normalised)
            .FirstOrDefaultAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        await _users.InsertOneAsync(user);
        return user;
    }

    public async Task<bool> UpdateAsync(User user)
    {
        var result = await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
        return result.ModifiedCount > 0;
    }
}
