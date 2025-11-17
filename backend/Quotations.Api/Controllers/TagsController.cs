using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

/// <summary>
/// DTO for tag with count
/// </summary>
public class TagDto
{
    public string Tag { get; set; } = string.Empty;
    public int Count { get; set; }
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
    public async Task<ActionResult<ApiResponse<List<TagDto>>>> GetTags([FromQuery] int limit = 100)
    {
        var tagsWithCounts = await _quotationRepository.GetTagsWithCountsAsync(limit);

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
}
