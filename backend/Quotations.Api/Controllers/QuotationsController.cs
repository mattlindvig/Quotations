using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

/// <summary>
/// API controller for quotation operations
/// </summary>
[ApiController]
[Route("api/v1/quotations")]
public class QuotationsController : ControllerBase
{
    private readonly QuotationService _quotationService;

    public QuotationsController(QuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    /// <summary>
    /// Get paginated list of quotations with optional filters
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <param name="authorId">Filter by author ID (optional)</param>
    /// <param name="sourceType">Filter by source type (optional)</param>
    /// <param name="tags">Filter by tags (comma-separated, optional)</param>
    /// <returns>Paginated list of quotations</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedQuotationsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedQuotationsResponse>>> GetQuotations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? authorId = null,
        [FromQuery] string? sourceType = null,
        [FromQuery] string? tags = null)
    {
        // Parse status
        QuotationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && System.Enum.TryParse<QuotationStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        // Parse source type
        SourceType? sourceTypeFilter = null;
        if (!string.IsNullOrEmpty(sourceType) && System.Enum.TryParse<SourceType>(sourceType, true, out var parsedSourceType))
        {
            sourceTypeFilter = parsedSourceType;
        }

        // Parse tags
        List<string>? tagsList = null;
        if (!string.IsNullOrEmpty(tags))
        {
            tagsList = new List<string>(tags.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries));
        }

        var result = await _quotationService.GetQuotationsAsync(
            page, pageSize, statusFilter, authorId, sourceTypeFilter, tagsList);

        return Ok(new ApiResponse<PaginatedQuotationsResponse>
        {
            Data = result,
            Success = true
        });
    }

    /// <summary>
    /// Get a single quotation by ID
    /// </summary>
    /// <param name="id">Quotation ID</param>
    /// <returns>Quotation details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> GetQuotationById(string id)
    {
        var quotation = await _quotationService.GetQuotationByIdAsync(id);

        if (quotation == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "id", new[] { $"Quotation with ID '{id}' not found" } }
                }
            });
        }

        return Ok(new ApiResponse<QuotationDto>
        {
            Data = quotation,
            Success = true
        });
    }

    /// <summary>
    /// Search quotations by text
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <returns>Paginated search results</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedQuotationsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedQuotationsResponse>>> SearchQuotations(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "q", new[] { "Search query is required" } }
                }
            });
        }

        var result = await _quotationService.SearchQuotationsAsync(q, page, pageSize);

        return Ok(new ApiResponse<PaginatedQuotationsResponse>
        {
            Data = result,
            Success = true
        });
    }
}
