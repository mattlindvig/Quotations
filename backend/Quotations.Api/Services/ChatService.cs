using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quotations.Api.Models;
using Quotations.Api.Models.Dtos;
using Quotations.Api.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Quotations.Api.Services;

public record ChatResult(string Reply, List<QuotationDto> Quotations);

public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly IQuotationRepository _quotationRepository;
    private readonly string _apiKey;
    private readonly ILogger<ChatService> _logger;
    private const string Model = "claude-haiku-4-5-20251001";
    private const string ApiVersion = "2023-06-01";
    private const string SystemPrompt =
        "You are a helpful assistant for a quotations library. Your role is to help users find " +
        "quotations that match their interests, themes, or descriptions. When a user asks for " +
        "quotations about a topic, use your search tools to find relevant examples from the database. " +
        "Always use the search tools to find real quotations rather than inventing them. " +
        "Be conversational and briefly explain why the quotes you found are relevant. " +
        "If no results are found, say so and suggest the user try different keywords.";

    public ChatService(
        HttpClient httpClient,
        IQuotationRepository quotationRepository,
        IConfiguration configuration,
        ILogger<ChatService> logger)
    {
        _httpClient = httpClient;
        _quotationRepository = quotationRepository;
        _apiKey = configuration["Anthropic:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<ChatResult> ChatAsync(string userMessage, List<ChatMessageDto> history)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            return new ChatResult("AI chat is not available — API key not configured.", new List<QuotationDto>());

        var foundQuotations = new Dictionary<string, QuotationDto>();

        // Build messages: prior history (text only) + new user message
        var messages = new List<object>();
        foreach (var msg in history)
            messages.Add(new { role = msg.Role, content = msg.Content });
        messages.Add(new { role = "user", content = userMessage });

        var tools = BuildToolDefinitions();

        // Agentic loop: keep calling the API until we get a final text reply
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var requestBody = new
            {
                model = Model,
                max_tokens = 1024,
                system = SystemPrompt,
                tools,
                messages
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", ApiVersion);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.SendAsync(request);
            var body = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API returned {Status}: {Body}", httpResponse.StatusCode, body);
                return new ChatResult("I encountered an error. Please try again.", new List<QuotationDto>());
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var stopReason = root.GetProperty("stop_reason").GetString();
            var contentArray = root.GetProperty("content");

            if (stopReason == "end_turn")
            {
                var reply = string.Empty;
                foreach (var block in contentArray.EnumerateArray())
                {
                    if (block.GetProperty("type").GetString() == "text")
                    {
                        reply = block.GetProperty("text").GetString() ?? string.Empty;
                        break;
                    }
                }
                return new ChatResult(reply, foundQuotations.Values.ToList());
            }

            if (stopReason != "tool_use")
                break;

            // Append assistant turn (with tool_use blocks) to the conversation
            var assistantContent = JsonNode.Parse(contentArray.GetRawText())!;
            messages.Add(new { role = "assistant", content = assistantContent });

            // Execute each tool and collect results
            var toolResults = new List<object>();
            foreach (var block in contentArray.EnumerateArray())
            {
                if (block.GetProperty("type").GetString() != "tool_use")
                    continue;

                var toolId = block.GetProperty("id").GetString()!;
                var toolName = block.GetProperty("name").GetString()!;
                var toolInput = block.GetProperty("input");

                string resultContent;
                try
                {
                    resultContent = await ExecuteToolAsync(toolName, toolInput, foundQuotations);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool {ToolName} threw an exception", toolName);
                    resultContent = "Tool execution failed.";
                }

                toolResults.Add(new { type = "tool_result", tool_use_id = toolId, content = resultContent });
            }

            messages.Add(new { role = "user", content = toolResults });
        }

        return new ChatResult("I couldn't process your request. Please try again.", new List<QuotationDto>());
    }

    private async Task<string> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        Dictionary<string, QuotationDto> found)
    {
        return toolName switch
        {
            "search_quotations" => await SearchQuotationsAsync(input, found),
            "get_random_quotation" => await GetRandomQuotationAsync(found),
            _ => "Unknown tool."
        };
    }

    private async Task<string> SearchQuotationsAsync(JsonElement input, Dictionary<string, QuotationDto> found)
    {
        string? query = input.TryGetProperty("query", out var q) ? q.GetString() : null;
        string? authorName = input.TryGetProperty("authorName", out var a) ? a.GetString() : null;
        SourceType? sourceType = null;
        List<string>? tags = null;

        if (input.TryGetProperty("sourceType", out var st) &&
            Enum.TryParse<SourceType>(st.GetString(), true, out var parsedSt))
            sourceType = parsedSt;

        if (input.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            tags = tagsEl.EnumerateArray()
                         .Select(t => t.GetString())
                         .Where(t => !string.IsNullOrWhiteSpace(t))
                         .Select(t => t!)
                         .ToList();

        List<Quotation> items;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var (searchItems, _) = await _quotationRepository.SearchQuotationsAsync(
                query, page: 1, pageSize: 5, status: QuotationStatus.Approved);
            items = searchItems;
        }
        else
        {
            var (filterItems, _) = await _quotationRepository.GetQuotationsAsync(
                page: 1, pageSize: 5,
                status: QuotationStatus.Approved,
                authorName: authorName,
                sourceType: sourceType,
                tags: tags);
            items = filterItems;
        }

        foreach (var item in items.Where(i => !found.ContainsKey(i.Id)))
            found[item.Id] = MapToDto(item);

        if (items.Count == 0)
            return "No quotations found matching those criteria.";

        var summary = items.Select(q => new
        {
            id = q.Id,
            text = q.Text,
            author = q.Author.Name,
            source = q.Source.Title,
            sourceType = q.Source.Type.ToString(),
            tags = q.Tags
        });

        return JsonSerializer.Serialize(summary);
    }

    private async Task<string> GetRandomQuotationAsync(Dictionary<string, QuotationDto> found)
    {
        var quotation = await _quotationRepository.GetRandomQuotationAsync();
        if (quotation == null)
            return "No quotations available.";

        if (!found.ContainsKey(quotation.Id))
            found[quotation.Id] = MapToDto(quotation);

        return JsonSerializer.Serialize(new
        {
            id = quotation.Id,
            text = quotation.Text,
            author = quotation.Author.Name,
            source = quotation.Source.Title,
            sourceType = quotation.Source.Type.ToString(),
            tags = quotation.Tags
        });
    }

    private static QuotationDto MapToDto(Quotation q) => new()
    {
        Id = q.Id,
        Text = q.Text,
        Author = new AuthorDto { Id = q.Author.Id.ToString(), Name = q.Author.Name },
        Source = new SourceDto
        {
            Id = q.Source.Id.ToString(),
            Title = q.Source.Title,
            Type = q.Source.Type.ToString()
        },
        Tags = q.Tags,
        Status = q.Status.ToString(),
        SubmittedAt = q.SubmittedAt
    };

    private static object[] BuildToolDefinitions() =>
    [
        new
        {
            name = "search_quotations",
            description = "Search for quotations in the database by keyword, author name, tags, or source type. Returns up to 5 matching approved quotations.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "Keywords to search for within quotation text"
                    },
                    authorName = new
                    {
                        type = "string",
                        description = "Filter by author name (partial match)"
                    },
                    tags = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "Filter by theme tags, e.g. [\"wisdom\", \"courage\"]"
                    },
                    sourceType = new
                    {
                        type = "string",
                        @enum = new[] { "Book", "Movie", "Speech", "Interview", "Other" },
                        description = "Filter by source type"
                    }
                }
            }
        },
        new
        {
            name = "get_random_quotation",
            description = "Return a random approved quotation. Use when the user wants a surprise quote or has no specific criteria.",
            input_schema = new
            {
                type = "object",
                properties = new { }
            }
        }
    ];
}
