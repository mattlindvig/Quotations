using Quotations.Api.Models;

namespace Quotations.Api.Repositories;

public interface IWikiquoteSyncRepository
{
    Task<WikiquoteSyncRecord?> GetLastCompletedAsync(WikiquoteSyncType? type = null);
    Task<WikiquoteSyncRecord?> GetLastAsync(WikiquoteSyncType type);
    Task<WikiquoteSyncRecord?> GetRunningAsync();
    Task<WikiquoteSyncRecord> CreateAsync(WikiquoteSyncRecord record);
    Task UpdateAsync(WikiquoteSyncRecord record);
    Task<List<WikiquoteSyncRecord>> GetRecentAsync(int limit = 10);
}
