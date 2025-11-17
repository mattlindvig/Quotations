using Quotations.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

/// <summary>
/// Repository interface for author data access
/// </summary>
public interface IAuthorRepository
{
    /// <summary>
    /// Get all authors with optional pagination
    /// </summary>
    Task<List<Author>> GetAuthorsAsync(int? limit = null);

    /// <summary>
    /// Get author by ID
    /// </summary>
    Task<Author?> GetAuthorByIdAsync(string id);

    /// <summary>
    /// Get author by name (case-insensitive)
    /// </summary>
    Task<Author?> GetAuthorByNameAsync(string name);

    /// <summary>
    /// Create a new author
    /// </summary>
    Task<Author> CreateAuthorAsync(Author author);

    /// <summary>
    /// Update an existing author
    /// </summary>
    Task<bool> UpdateAuthorAsync(Author author);
}
