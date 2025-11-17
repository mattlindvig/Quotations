using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

/// <summary>
/// API controller for source operations
/// </summary>
[ApiController]
[Route("api/v1/sources")]
public class SourcesController : ControllerBase
{
    private readonly ISourceRepository _sourceRepository;

    public SourcesController(ISourceRepository sourceRepository)
    {
        _sourceRepository = sourceRepository;
    }

    /// <summary>
    /// Get list of sources
    /// </summary>
    /// <param name="type">Filter by source type (optional)</param>
    /// <param name="limit">Maximum number of sources to return (optional)</param>
    /// <returns>List of sources</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<Source>>), 200)]
    public async Task<ActionResult<ApiResponse<List<Source>>>> GetSources(
        [FromQuery] string? type = null,
        [FromQuery] int? limit = null)
    {
        SourceType? sourceType = null;
        if (!string.IsNullOrEmpty(type) && System.Enum.TryParse<SourceType>(type, true, out var parsedType))
        {
            sourceType = parsedType;
        }

        var sources = await _sourceRepository.GetSourcesAsync(sourceType, limit);

        return Ok(new ApiResponse<List<Source>>
        {
            Data = sources,
            Success = true
        });
    }

    /// <summary>
    /// Get a single source by ID
    /// </summary>
    /// <param name="id">Source ID</param>
    /// <returns>Source details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Source>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<Source>>> GetSourceById(string id)
    {
        var source = await _sourceRepository.GetSourceByIdAsync(id);

        if (source == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "id", new[] { $"Source with ID '{id}' not found" } }
                }
            });
        }

        return Ok(new ApiResponse<Source>
        {
            Data = source,
            Success = true
        });
    }
}
