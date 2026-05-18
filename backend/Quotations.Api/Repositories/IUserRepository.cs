using Quotations.Api.Models;

namespace Quotations.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<List<User>> GetAllAsync();
    Task<bool> UpdateRolesAsync(string userId, List<string> roles);
    Task<List<string>> GetFavoriteIdsAsync(string userId);
    Task<bool> AddFavoriteAsync(string userId, string quotationId);
    Task<bool> RemoveFavoriteAsync(string userId, string quotationId);
    Task IncrementFailedLoginAsync(string userId, DateTime? lockoutUntil);
    Task ResetFailedLoginAsync(string userId);
    Task SetEmailVerificationTokenAsync(string userId, string hashedToken, DateTime expiry);
    Task<bool> VerifyEmailAsync(string hashedToken);
    Task SetPasswordResetTokenAsync(string userId, string hashedToken, DateTime expiry);
    Task<bool> ResetPasswordAsync(string hashedToken, string newPasswordHash);
}
