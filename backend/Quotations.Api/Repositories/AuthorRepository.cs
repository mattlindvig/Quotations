using MongoDB.Bson;
using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

/// <summary>
/// MongoDB implementation of author repository
/// </summary>
public class AuthorRepository : IAuthorRepository
{
    private readonly IMongoCollection<Author> _authors;

    public AuthorRepository(MongoDbService mongoDbService)
    {
        _authors = mongoDbService.GetCollection<Author>("authors");
    }

    public async Task<List<Author>> GetAuthorsAsync(int? limit = null)
    {
        IFindFluent<Author, Author> query = _authors
            .Find(FilterDefinition<Author>.Empty)
            .SortBy(a => a.Name);

        if (limit.HasValue)
        {
            query = query.Limit(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Author?> GetAuthorByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _authors
            .Find(a => a.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Author?> GetAuthorByNameAsync(string name)
    {
        return await _authors
            .Find(a => a.Name.ToLower() == name.ToLower())
            .FirstOrDefaultAsync();
    }

    public async Task<Author> CreateAuthorAsync(Author author)
    {
        author.CreatedAt = DateTime.UtcNow;
        author.UpdatedAt = DateTime.UtcNow;

        await _authors.InsertOneAsync(author);
        return author;
    }

    public async Task<bool> UpdateAuthorAsync(Author author)
    {
        author.UpdatedAt = DateTime.UtcNow;

        var result = await _authors.ReplaceOneAsync(
            a => a.Id == author.Id,
            author);

        return result.ModifiedCount > 0;
    }
}
