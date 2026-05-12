using Quotations.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

public interface IAiBatchJobRepository
{
    Task<AiBatchJob> CreateAsync(AiBatchJob job);
    Task<AiBatchJob?> GetByIdAsync(string id);
    Task<AiBatchJob?> GetByAnthropicBatchIdAsync(string anthropicBatchId);
    Task<List<AiBatchJob>> GetPendingJobsAsync();
    Task<bool> UpdateAsync(AiBatchJob job);
    Task<List<AiBatchJob>> GetRecentAsync(int limit = 20);
}
