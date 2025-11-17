using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Services;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

/// <summary>
/// API controller for quotation review workflow
/// </summary>
[ApiController]
[Route("api/v1/review")]
[Authorize(Roles = "Reviewer,Admin")]
public class ReviewController : ControllerBase
{
    private readonly QuotationService _quotationService;

    public ReviewController(QuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    /// <summary>
    /// Get pending quotations for review
    /// </summary>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Items per page</param>
    /// <returns>Paginated list of pending quotations</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedQuotationsResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<ApiResponse<PaginatedQuotationsResponse>>> GetPendingQuotations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var pendingQuotations = await _quotationService.GetPendingQuotationsAsync(page, pageSize);

        return Ok(new ApiResponse<PaginatedQuotationsResponse>
        {
            Data = pendingQuotations,
            Success = true
        });
    }

    /// <summary>
    /// Approve a pending quotation
    /// </summary>
    /// <param name="id">Quotation ID</param>
    /// <param name="request">Approval details</param>
    /// <returns>Updated quotation</returns>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> ApproveQuotation(
        string id,
        [FromBody] ApproveQuotationRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var quotation = await _quotationService.ApproveQuotationAsync(id, userId, username, request.ReviewerNotes);

            return Ok(new ApiResponse<QuotationDto>
            {
                Data = quotation,
                Success = true
            });
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "general", new[] { ex.Message } }
                }
            });
        }
        catch (System.ArgumentException ex)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "general", new[] { ex.Message } }
                }
            });
        }
    }

    /// <summary>
    /// Reject a pending quotation
    /// </summary>
    /// <param name="id">Quotation ID</param>
    /// <param name="request">Rejection details</param>
    /// <returns>Updated quotation</returns>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> RejectQuotation(
        string id,
        [FromBody] RejectQuotationRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var entry in ModelState)
            {
                if (entry.Value.Errors.Count > 0)
                {
                    errors[entry.Key] = entry.Value.Errors.Select(e => e.ErrorMessage).ToArray();
                }
            }

            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = errors
            });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var quotation = await _quotationService.RejectQuotationAsync(
                id, userId, username, request.RejectionReason, request.ReviewerNotes);

            return Ok(new ApiResponse<QuotationDto>
            {
                Data = quotation,
                Success = true
            });
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "general", new[] { ex.Message } }
                }
            });
        }
        catch (System.ArgumentException ex)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "general", new[] { ex.Message } }
                }
            });
        }
    }

    /// <summary>
    /// Check for potential duplicates of a quotation
    /// </summary>
    /// <param name="id">Quotation ID</param>
    /// <returns>List of potential duplicate quotations</returns>
    [HttpGet("{id}/duplicates")]
    [ProducesResponseType(typeof(ApiResponse<List<QuotationDto>>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<List<QuotationDto>>>> GetPotentialDuplicates(string id)
    {
        try
        {
            var duplicates = await _quotationService.GetPotentialDuplicatesAsync(id);

            return Ok(new ApiResponse<List<QuotationDto>>
            {
                Data = duplicates,
                Success = true
            });
        }
        catch (System.ArgumentException ex)
        {
            return NotFound(new ApiResponse<List<QuotationDto>>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "general", new[] { ex.Message } }
                }
            });
        }
    }
}