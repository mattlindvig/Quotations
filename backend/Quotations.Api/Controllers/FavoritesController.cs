using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Repositories;
using Quotations.Api.Services;
using System.Security.Claims;

namespace Quotations.Api.Controllers;

[ApiController]
[Route("api/v1/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly QuotationService _quotationService;

    public FavoritesController(IUserRepository userRepository, QuotationService quotationService)
    {
        _userRepository = userRepository;
        _quotationService = quotationService;
    }

    /// <summary>
    /// Get all favorite quotation IDs for the current user.
    /// </summary>
    [HttpGet("ids")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), 200)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetFavoriteIds()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.ErrorResponse("User not authenticated."));

        var ids = await _userRepository.GetFavoriteIdsAsync(userId);
        return Ok(ApiResponse<List<string>>.SuccessResponse(ids));
    }

    /// <summary>
    /// Get paginated list of favorited quotations for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedQuotationsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedQuotationsResponse>>> GetFavorites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.ErrorResponse("User not authenticated."));

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var allIds = await _userRepository.GetFavoriteIdsAsync(userId);
        var totalCount = allIds.Count;

        var pagedIds = allIds
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var items = await _quotationService.GetByIdsAsync(pagedIds);

        var response = new PaginatedQuotationsResponse
        {
            Items = items,
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            }
        };

        return Ok(ApiResponse<PaginatedQuotationsResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// Add a quotation to the current user's favorites.
    /// </summary>
    [HttpPost("{quotationId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> AddFavorite(string quotationId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.ErrorResponse("User not authenticated."));

        var quotation = await _quotationService.GetQuotationByIdAsync(quotationId);
        if (quotation is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Quotation '{quotationId}' not found."));

        await _userRepository.AddFavoriteAsync(userId, quotationId);
        return Ok(ApiResponse<object>.SuccessResponse(new object(), "Added to favorites."));
    }

    /// <summary>
    /// Remove a quotation from the current user's favorites.
    /// </summary>
    [HttpDelete("{quotationId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFavorite(string quotationId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.ErrorResponse("User not authenticated."));

        await _userRepository.RemoveFavoriteAsync(userId, quotationId);
        return Ok(ApiResponse<object>.SuccessResponse(new object(), "Removed from favorites."));
    }
}
