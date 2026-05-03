using Quotations.Api.Models;

namespace Quotations.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<List<string>> GetFavoriteIdsAsync(string userId);
    Task<bool> AddFavoriteAsync(string userId, string quotationId);
    Task<bool> RemoveFavoriteAsync(string userId, string quotationId);
}
