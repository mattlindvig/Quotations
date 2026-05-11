using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

public class QuoteOfDayRepository : IQuoteOfDayRepository
{
    private readonly IMongoCollection<QuoteOfDay> _collection;

    public QuoteOfDayRepository(MongoDbService mongoDbService)
    {
        _collection = mongoDbService.GetCollection<QuoteOfDay>("quoteOfDay");
    }

    public async Task<QuoteOfDay?> GetByDateAsync(string date)
        => await _collection.Find(q => q.Date == date).FirstOrDefaultAsync();

    public async Task CreateAsync(QuoteOfDay entry)
        => await _collection.InsertOneAsync(entry);

    public async Task<List<string>> GetAllUsedQuotationIdsAsync()
    {
        var docs = await _collection.Find(_ => true).ToListAsync();
        return docs.Select(d => d.QuotationId).ToList();
    }
}
