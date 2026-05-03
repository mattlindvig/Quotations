using Quotations.Api.Models;

namespace Quotations.Api.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> CreateAsync(RefreshToken token);
    Task<RefreshToken?> FindByTokenAsync(string token);
    Task RevokeAsync(string token);
    Task RevokeAllForUserAsync(string userId);
}
