using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Services;

namespace Quotations.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("chat")]
public class ChatController : ControllerBase
{
    private const int MaxMessageLength = 2000;
    private const int MaxHistoryItems = 20;
    private const int MaxHistoryItemLength = 2000;

    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), 400)]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(ApiResponse<ChatResponse>.ErrorResponse("Message is required."));

        if (request.Message.Length > MaxMessageLength)
            return BadRequest(ApiResponse<ChatResponse>.ErrorResponse($"Message must be {MaxMessageLength} characters or fewer."));

        if (request.ConversationHistory.Count > MaxHistoryItems)
            return BadRequest(ApiResponse<ChatResponse>.ErrorResponse($"Conversation history cannot exceed {MaxHistoryItems} messages."));

        var invalidHistory = request.ConversationHistory.FirstOrDefault(
            m => m.Role != "user" && m.Role != "assistant");
        if (invalidHistory is not null)
            return BadRequest(ApiResponse<ChatResponse>.ErrorResponse("Conversation history contains an invalid role."));

        var oversizedHistory = request.ConversationHistory.FirstOrDefault(
            m => m.Content.Length > MaxHistoryItemLength);
        if (oversizedHistory is not null)
            return BadRequest(ApiResponse<ChatResponse>.ErrorResponse($"Each history message must be {MaxHistoryItemLength} characters or fewer."));

        var result = await _chatService.ChatAsync(request.Message, request.ConversationHistory);

        return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse
        {
            Reply = result.Reply,
            Quotations = result.Quotations
        }));
    }
}
