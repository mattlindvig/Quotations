using Quotations.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

public interface IAiReviewErrorRepository
{
    Task<AiReviewError> CreateAsync(AiReviewError error);
    Task<List<AiReviewError>> GetAllAsync(int page = 1, int pageSize = 50);
    Task<long> CountAsync();
    Task<bool> DeleteByQuotationIdAsync(string quotationId);
    Task<long> DeleteAllAsync();
}
