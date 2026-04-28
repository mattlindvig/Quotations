using System.Collections.Generic;

namespace Quotations.Api.Models.Dtos;

public class BulkSubmitQuotationRequest
{
    public List<SubmitQuotationRequest> Quotations { get; set; } = new();
    public string Mode { get; set; } = "async";
}

public class BulkSubmitResult
{
    public int Accepted { get; set; }
    public int Failed { get; set; }
    public List<BulkSubmitItemResult> Results { get; set; } = new();
}

public class BulkSubmitItemResult
{
    public int Index { get; set; }
    public bool Success { get; set; }
    public QuotationDto? Quotation { get; set; }
    public string? Error { get; set; }
}
