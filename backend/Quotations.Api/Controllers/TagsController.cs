using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

public class TagDto
{
    public string Tag { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CanonicalTagCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// API controller for tag operations
/// </summary>
[ApiController]
[Route("api/v1/tags")]
public class TagsController : ControllerBase
{
    private readonly IQuotationRepository _quotationRepository;

    public TagsController(IQuotationRepository quotationRepository)
    {
        _quotationRepository = quotationRepository;
    }

    /// <summary>
    /// Get list of tags with usage counts
    /// </summary>
    /// <param name="limit">Maximum number of tags to return (default: 100)</param>
    /// <returns>List of tags with their usage counts</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TagDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<TagDto>>>> GetTags(
        [FromQuery] int limit = 100,
        [FromQuery] string? authorName = null,
        [FromQuery] string? sourceType = null)
    {
        SourceType? sourceTypeFilter = null;
        if (!string.IsNullOrEmpty(sourceType) && System.Enum.TryParse<SourceType>(sourceType, true, out var parsed))
            sourceTypeFilter = parsed;

        var tagsWithCounts = await _quotationRepository.GetTagsWithCountsAsync(limit, authorName, sourceTypeFilter);

        var tagDtos = tagsWithCounts.Select(t => new TagDto
        {
            Tag = t.Tag,
            Count = t.Count
        }).ToList();

        return Ok(new ApiResponse<List<TagDto>>
        {
            Data = tagDtos,
            Success = true
        });
    }

    /// <summary>
    /// Get the canonical tag taxonomy grouped by category
    /// </summary>
    [HttpGet("canonical")]
    [ProducesResponseType(typeof(ApiResponse<List<CanonicalTagCategoryDto>>), 200)]
    public ActionResult<ApiResponse<List<CanonicalTagCategoryDto>>> GetCanonicalTags()
    {
        var categories = CanonicalTags.ByCategory
            .Select(kv => new CanonicalTagCategoryDto
            {
                Category = kv.Key,
                Tags = kv.Value.ToList()
            })
            .ToList();

        return Ok(new ApiResponse<List<CanonicalTagCategoryDto>>
        {
            Data = categories,
            Success = true
        });
    }
}
