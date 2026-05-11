using Quotations.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

public interface IQuoteOfDayRepository
{
    Task<QuoteOfDay?> GetByDateAsync(string date);
    Task CreateAsync(QuoteOfDay entry);
    Task<List<string>> GetAllUsedQuotationIdsAsync();
}
