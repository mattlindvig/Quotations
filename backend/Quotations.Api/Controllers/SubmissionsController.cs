using FluentValidation;
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
/// API controller for quotation submissions
/// </summary>
[ApiController]
[Route("api/v1/submissions")]
public class SubmissionsController : ControllerBase
{
    private readonly QuotationService _quotationService;
    private readonly IValidator<SubmitQuotationRequest> _validator;

    public SubmissionsController(
        QuotationService quotationService,
        IValidator<SubmitQuotationRequest> validator)
    {
        _quotationService = quotationService;
        _validator = validator;
    }

    /// <summary>
    /// Submit a new quotation for review
    /// </summary>
    /// <param name="request">Quotation submission data</param>
    /// <param name="mode">AI review mode: "sync" (wait for analysis) or "async" (background, default)</param>
    /// <returns>Created quotation with pending status</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> SubmitQuotation(
        [FromBody] SubmitQuotationRequest request,
        [FromQuery] string mode = "async")
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var error in validationResult.Errors)
            {
                if (!errors.ContainsKey(error.PropertyName))
                    errors[error.PropertyName] = new[] { error.ErrorMessage };
            }

            return BadRequest(new ApiResponse<object> { Success = false, Errors = errors });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        try
        {
            var quotation = await _quotationService.SubmitQuotationAsync(request, userId, username, mode);

            return CreatedAtAction(
                nameof(QuotationsController.GetQuotationById),
                "Quotations",
                new { id = quotation.Id },
                new ApiResponse<QuotationDto> { Data = quotation, Success = true });
        }
        catch (System.Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]> { { "general", new[] { ex.Message } } }
            });
        }
    }

    /// <summary>
    /// Submit multiple quotations in bulk — AI review always runs asynchronously
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponse<BulkSubmitResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<BulkSubmitResult>>> BulkSubmitQuotations(
        [FromBody] BulkSubmitQuotationRequest request)
    {
        if (request.Quotations == null || request.Quotations.Count == 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]> { { "quotations", new[] { "At least one quotation is required." } } }
            });
        }

        if (request.Quotations.Count > 500)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]> { { "quotations", new[] { "Maximum 500 quotations per bulk request." } } }
            });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        var result = await _quotationService.BulkSubmitQuotationsAsync(request, userId, username);

        return Ok(new ApiResponse<BulkSubmitResult> { Data = result, Success = true });
    }

    /// <summary>
    /// Get current user's submissions
    /// </summary>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Items per page</param>
    /// <returns>Paginated list of user's submissions</returns>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaginatedQuotationsResponse>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse<PaginatedQuotationsResponse>>> GetMySubmissions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var submissions = await _quotationService.GetUserSubmissionsAsync(userId, page, pageSize);

        return Ok(new ApiResponse<PaginatedQuotationsResponse>
        {
            Data = submissions,
            Success = true
        });
    }
}