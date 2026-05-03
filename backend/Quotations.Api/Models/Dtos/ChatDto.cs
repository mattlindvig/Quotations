using System.Collections.Generic;

namespace Quotations.Api.Models.Dtos;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto> ConversationHistory { get; set; } = new();
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<QuotationDto> Quotations { get; set; } = new();
}
