using Quotations.Api.Models;

namespace Quotations.Api.Repositories;

public interface IFavQsSyncRepository
{
    Task<FavQsSyncRecord?> GetLastCompletedAsync();
    Task<FavQsSyncRecord?> GetLastAsync();
    Task<FavQsSyncRecord?> GetRunningAsync();
    Task<FavQsSyncRecord> CreateAsync(FavQsSyncRecord record);
    Task UpdateAsync(FavQsSyncRecord record);
    Task<List<FavQsSyncRecord>> GetRecentAsync(int limit = 10);
}
