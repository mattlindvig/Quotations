namespace Quotations.Api.Models.Dtos;

/// <summary>
/// Request DTO for approving a quotation
/// </summary>
public class ApproveQuotationRequest
{
    /// <summary>
    /// Optional reviewer notes about the approval
    /// </summary>
    public string? ReviewerNotes { get; set; }
}