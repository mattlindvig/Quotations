using Microsoft.Extensions.Logging;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quotations.Api.Services;

/// <summary>
/// Business logic service for quotation operations
/// </summary>
public class QuotationService
{
    private readonly IQuotationRepository _quotationRepository;
    private readonly IAuthorRepository _authorRepository;
    private readonly ISourceRepository _sourceRepository;
    private readonly ILogger<QuotationService> _logger;

    public QuotationService(
        IQuotationRepository quotationRepository,
        IAuthorRepository authorRepository,
        ISourceRepository sourceRepository,
        ILogger<QuotationService> logger)
    {
        _quotationRepository = quotationRepository;
        _authorRepository = authorRepository;
        _sourceRepository = sourceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of quotations with optional filters
    /// </summary>
    public async Task<PaginatedQuotationsResponse> GetQuotationsAsync(
        int page = 1,
        int pageSize = 20,
        QuotationStatus? status = null,
        string? authorId = null,
        SourceType? sourceType = null,
        List<string>? tags = null)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Max page size limit

        var (items, totalCount) = await _quotationRepository.GetQuotationsAsync(
            page, pageSize, status, authorId, sourceType, tags);

        return new PaginatedQuotationsResponse
        {
            Items = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = (int)totalCount
            }
        };
    }

    /// <summary>
    /// Get a single quotation by ID
    /// </summary>
    public async Task<QuotationDto?> GetQuotationByIdAsync(string id)
    {
        var quotation = await _quotationRepository.GetQuotationByIdAsync(id);
        return quotation != null ? MapToDto(quotation) : null;
    }

    /// <summary>
    /// Search quotations by text
    /// </summary>
    public async Task<PaginatedQuotationsResponse> SearchQuotationsAsync(
        string searchText,
        int page = 1,
        int pageSize = 20,
        QuotationStatus? status = null)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _quotationRepository.SearchQuotationsAsync(
            searchText, page, pageSize, status);

        return new PaginatedQuotationsResponse
        {
            Items = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = (int)totalCount
            }
        };
    }

    /// <summary>
    /// Create a new quotation submission
    /// </summary>
    public async Task<QuotationDto> CreateQuotationAsync(Quotation quotation, string? userId = null)
    {
        // Set submission metadata
        quotation.Status = QuotationStatus.Pending;
        quotation.SubmittedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(userId))
        {
            quotation.SubmittedBy = new UserReference
            {
                Id = userId,
                Username = "User" // TODO: Fetch username from Identity
            };
        }

        var created = await _quotationRepository.CreateQuotationAsync(quotation);
        return MapToDto(created);
    }

    /// <summary>
    /// Submit a new quotation with author/source lookup or creation
    /// </summary>
    public async Task<QuotationDto> SubmitQuotationAsync(SubmitQuotationRequest request, string? userId = null, string? username = null)
    {
        _logger.LogInformation("Submitting new quotation by user {Username} (ID: {UserId})", username ?? "Anonymous", userId ?? "N/A");

        // Get or create author
        var author = await _authorRepository.GetAuthorByNameAsync(request.AuthorName);
        if (author == null)
        {
            _logger.LogInformation("Creating new author: {AuthorName}", request.AuthorName);
            author = new Author
            {
                Name = request.AuthorName,
                Lifespan = request.AuthorLifespan,
                Occupation = request.AuthorOccupation
            };
            author = await _authorRepository.CreateAuthorAsync(author);
        }

        // Parse source type
        if (!Enum.TryParse<SourceType>(request.SourceType, true, out var sourceType))
        {
            _logger.LogWarning("Invalid source type provided: {SourceType}", request.SourceType);
            throw new ArgumentException($"Invalid source type: {request.SourceType}");
        }

        // Get or create source
        var source = await _sourceRepository.GetSourceByTitleAndTypeAsync(request.SourceTitle, sourceType);
        if (source == null)
        {
            _logger.LogInformation("Creating new source: {SourceTitle} ({SourceType})", request.SourceTitle, sourceType);
            source = new Source
            {
                Title = request.SourceTitle,
                Type = sourceType,
                Year = request.SourceYear,
                AdditionalInfo = request.SourceAdditionalInfo
            };
            source = await _sourceRepository.CreateSourceAsync(source);
        }

        // Create quotation
        var quotation = new Quotation
        {
            Text = request.Text.Trim(),
            Author = new AuthorReference
            {
                Id = author.Id,
                Name = author.Name
            },
            Source = new SourceReference
            {
                Id = source.Id,
                Title = source.Title,
                Type = source.Type
            },
            Tags = request.Tags.Select(t => t.Trim()).ToList(),
            Status = QuotationStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(userId))
        {
            quotation.SubmittedBy = new UserReference
            {
                Id = userId,
                Username = username ?? "User"
            };
        }

        var created = await _quotationRepository.CreateQuotationAsync(quotation);
        _logger.LogInformation("Successfully created quotation {QuotationId} by {AuthorName}", created.Id, author.Name);
        return MapToDto(created);
    }

    /// <summary>
    /// Get user's submitted quotations
    /// </summary>
    public async Task<PaginatedQuotationsResponse> GetUserSubmissionsAsync(
        string userId,
        int page = 1,
        int pageSize = 20)
    {
        // This would require a new repository method to filter by submittedBy.Id
        // For now, return empty - to be implemented
        return new PaginatedQuotationsResponse
        {
            Items = new List<QuotationDto>(),
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            }
        };
    }

    /// <summary>
    /// Get pending quotations for review
    /// </summary>
    public async Task<PaginatedQuotationsResponse> GetPendingQuotationsAsync(
        int page = 1,
        int pageSize = 20)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _quotationRepository.GetQuotationsAsync(
            page, pageSize, QuotationStatus.Pending, null, null, null);

        return new PaginatedQuotationsResponse
        {
            Items = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = (int)totalCount
            }
        };
    }

    /// <summary>
    /// Approve a quotation
    /// </summary>
    public async Task<QuotationDto> ApproveQuotationAsync(
        string id,
        string reviewerId,
        string? reviewerUsername,
        string? reviewerNotes)
    {
        _logger.LogInformation("Reviewer {ReviewerUsername} (ID: {ReviewerId}) approving quotation {QuotationId}",
            reviewerUsername ?? "Unknown", reviewerId, id);

        var quotation = await _quotationRepository.GetQuotationByIdAsync(id);
        if (quotation == null)
        {
            _logger.LogWarning("Attempted to approve non-existent quotation {QuotationId}", id);
            throw new ArgumentException($"Quotation with ID {id} not found");
        }

        if (quotation.Status != QuotationStatus.Pending)
        {
            _logger.LogWarning("Attempted to approve quotation {QuotationId} with invalid status {Status}", id, quotation.Status);
            throw new InvalidOperationException($"Cannot approve quotation with status {quotation.Status}");
        }

        quotation.Status = QuotationStatus.Approved;
        quotation.ReviewedBy = new UserReference
        {
            Id = reviewerId,
            Username = reviewerUsername ?? "Reviewer"
        };
        quotation.ReviewedAt = DateTime.UtcNow;
        quotation.UpdatedAt = DateTime.UtcNow;

        await _quotationRepository.UpdateQuotationAsync(quotation);
        _logger.LogInformation("Successfully approved quotation {QuotationId} by {ReviewerUsername}", id, reviewerUsername ?? "Unknown");
        return MapToDto(quotation);
    }

    /// <summary>
    /// Reject a quotation
    /// </summary>
    public async Task<QuotationDto> RejectQuotationAsync(
        string id,
        string reviewerId,
        string? reviewerUsername,
        string rejectionReason,
        string? reviewerNotes)
    {
        _logger.LogInformation("Reviewer {ReviewerUsername} (ID: {ReviewerId}) rejecting quotation {QuotationId}. Reason: {RejectionReason}",
            reviewerUsername ?? "Unknown", reviewerId, id, rejectionReason);

        var quotation = await _quotationRepository.GetQuotationByIdAsync(id);
        if (quotation == null)
        {
            _logger.LogWarning("Attempted to reject non-existent quotation {QuotationId}", id);
            throw new ArgumentException($"Quotation with ID {id} not found");
        }

        if (quotation.Status != QuotationStatus.Pending)
        {
            _logger.LogWarning("Attempted to reject quotation {QuotationId} with invalid status {Status}", id, quotation.Status);
            throw new InvalidOperationException($"Cannot reject quotation with status {quotation.Status}");
        }

        quotation.Status = QuotationStatus.Rejected;
        quotation.ReviewedBy = new UserReference
        {
            Id = reviewerId,
            Username = reviewerUsername ?? "Reviewer"
        };
        quotation.ReviewedAt = DateTime.UtcNow;
        quotation.RejectionReason = rejectionReason;
        quotation.UpdatedAt = DateTime.UtcNow;

        await _quotationRepository.UpdateQuotationAsync(quotation);
        _logger.LogInformation("Successfully rejected quotation {QuotationId} by {ReviewerUsername}", id, reviewerUsername ?? "Unknown");
        return MapToDto(quotation);
    }

    /// <summary>
    /// Get potential duplicates of a quotation
    /// </summary>
    public async Task<List<QuotationDto>> GetPotentialDuplicatesAsync(string id)
    {
        var quotation = await _quotationRepository.GetQuotationByIdAsync(id);
        if (quotation == null)
        {
            throw new ArgumentException($"Quotation with ID {id} not found");
        }

        var duplicates = await _quotationRepository.FindPotentialDuplicatesAsync(
            quotation.Text, quotation.Author.Id, quotation.Source.Id, id);

        return duplicates.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Map Quotation entity to DTO
    /// </summary>
    private static QuotationDto MapToDto(Quotation quotation)
    {
        return new QuotationDto
        {
            Id = quotation.Id,
            Text = quotation.Text,
            Author = new AuthorDto
            {
                Id = quotation.Author.Id,
                Name = quotation.Author.Name
            },
            Source = new SourceDto
            {
                Id = quotation.Source.Id,
                Title = quotation.Source.Title,
                Type = quotation.Source.Type.ToString().ToLowerInvariant()
            },
            Tags = quotation.Tags,
            Status = quotation.Status.ToString().ToLowerInvariant(),
            SubmittedAt = quotation.SubmittedAt,
            ReviewedAt = quotation.ReviewedAt
        };
    }
}
