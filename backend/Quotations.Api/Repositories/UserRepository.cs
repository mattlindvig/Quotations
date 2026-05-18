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

    public async Task<List<User>> GetAllAsync()
    {
        return await _users.Find(_ => true).SortBy(u => u.Username).ToListAsync();
    }

    public async Task<bool> UpdateRolesAsync(string userId, List<string> roles)
    {
        if (!ObjectId.TryParse(userId, out _))
            return false;

        var update = Builders<User>.Update.Set(u => u.Roles, roles);
        var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
        return result.MatchedCount > 0;
    }

    public async Task<List<string>> GetFavoriteIdsAsync(string userId)
    {
        if (!ObjectId.TryParse(userId, out _))
            return new List<string>();

        var user = await _users
            .Find(u => u.Id == userId)
            .Project(u => u.FavoriteQuotationIds)
            .FirstOrDefaultAsync();

        return user ?? new List<string>();
    }

    public async Task<bool> AddFavoriteAsync(string userId, string quotationId)
    {
        if (!ObjectId.TryParse(userId, out _))
            return false;

        var update = Builders<User>.Update.AddToSet(u => u.FavoriteQuotationIds, quotationId);
        var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task<bool> RemoveFavoriteAsync(string userId, string quotationId)
    {
        if (!ObjectId.TryParse(userId, out _))
            return false;

        var update = Builders<User>.Update.Pull(u => u.FavoriteQuotationIds, quotationId);
        var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task IncrementFailedLoginAsync(string userId, DateTime? lockoutUntil)
    {
        var update = lockoutUntil.HasValue
            ? Builders<User>.Update.Inc(u => u.FailedLoginCount, 1).Set(u => u.LockoutUntil, lockoutUntil.Value)
            : Builders<User>.Update.Inc(u => u.FailedLoginCount, 1);
        await _users.UpdateOneAsync(u => u.Id == userId, update);
    }

    public async Task ResetFailedLoginAsync(string userId)
    {
        var update = Builders<User>.Update
            .Set(u => u.FailedLoginCount, 0)
            .Unset(u => u.LockoutUntil);
        await _users.UpdateOneAsync(u => u.Id == userId, update);
    }

    public async Task SetEmailVerificationTokenAsync(string userId, string hashedToken, DateTime expiry)
    {
        var update = Builders<User>.Update
            .Set(u => u.EmailVerificationToken, hashedToken)
            .Set(u => u.EmailVerificationExpiry, expiry);
        await _users.UpdateOneAsync(u => u.Id == userId, update);
    }

    public async Task<bool> VerifyEmailAsync(string hashedToken)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.EmailVerificationToken, hashedToken),
            Builders<User>.Filter.Gt(u => u.EmailVerificationExpiry, DateTime.UtcNow)
        );
        var update = Builders<User>.Update
            .Set(u => u.EmailVerified, true)
            .Unset(u => u.EmailVerificationToken)
            .Unset(u => u.EmailVerificationExpiry);
        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task SetPasswordResetTokenAsync(string userId, string hashedToken, DateTime expiry)
    {
        var update = Builders<User>.Update
            .Set(u => u.PasswordResetToken, hashedToken)
            .Set(u => u.PasswordResetExpiry, expiry);
        await _users.UpdateOneAsync(u => u.Id == userId, update);
    }

    public async Task<bool> ResetPasswordAsync(string hashedToken, string newPasswordHash)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.PasswordResetToken, hashedToken),
            Builders<User>.Filter.Gt(u => u.PasswordResetExpiry, DateTime.UtcNow)
        );
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, newPasswordHash)
            .Unset(u => u.PasswordResetToken)
            .Unset(u => u.PasswordResetExpiry)
            .Set(u => u.FailedLoginCount, 0)
            .Unset(u => u.LockoutUntil);
        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
}
