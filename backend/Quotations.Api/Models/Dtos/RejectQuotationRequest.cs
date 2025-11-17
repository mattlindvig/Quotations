using System.ComponentModel.DataAnnotations;

namespace Quotations.Api.Models.Dtos;

/// <summary>
/// Request DTO for rejecting a quotation
/// </summary>
public class RejectQuotationRequest
{
    /// <summary>
    /// Reason for rejection (required)
    /// </summary>
    [Required(ErrorMessage = "Rejection reason is required")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Rejection reason must be between 10 and 1000 characters")]
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// Optional reviewer notes
    /// </summary>
    public string? ReviewerNotes { get; set; }
}