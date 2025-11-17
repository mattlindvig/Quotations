using Quotations.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

/// <summary>
/// Repository interface for source data access
/// </summary>
public interface ISourceRepository
{
    /// <summary>
    /// Get all sources with optional type filter and pagination
    /// </summary>
    Task<List<Source>> GetSourcesAsync(SourceType? type = null, int? limit = null);

    /// <summary>
    /// Get source by ID
    /// </summary>
    Task<Source?> GetSourceByIdAsync(string id);

    /// <summary>
    /// Get source by title and type (case-insensitive)
    /// </summary>
    Task<Source?> GetSourceByTitleAndTypeAsync(string title, SourceType type);

    /// <summary>
    /// Create a new source
    /// </summary>
    Task<Source> CreateSourceAsync(Source source);

    /// <summary>
    /// Update an existing source
    /// </summary>
    Task<bool> UpdateSourceAsync(Source source);
}
