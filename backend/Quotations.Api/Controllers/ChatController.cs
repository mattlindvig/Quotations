using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Services;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
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

        var result = await _chatService.ChatAsync(request.Message, request.ConversationHistory);

        return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse
        {
            Reply = result.Reply,
            Quotations = result.Quotations
        }));
    }
}
