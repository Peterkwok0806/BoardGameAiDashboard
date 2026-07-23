using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Features.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Uses the LLM (via Semantic Kernel) to rewrite a user's follow-up query
/// into a standalone search query for vector search. This replaces the naive
/// history-concatenation approach with a dedicated query rewriting step.
/// </summary>
public sealed class LlmQueryRewriter : IQueryRewriter
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<LlmQueryRewriter> _logger;

    public LlmQueryRewriter(
        IChatCompletionService chatCompletionService,
        ILogger<LlmQueryRewriter> logger)
    {
        _chatCompletionService = chatCompletionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> RewriteAsync(
        string query,
        IReadOnlyList<ChatMessageDto> history,
        string? gameName = null,
        CancellationToken cancellationToken = default)
    {
        // If no history, the query is already standalone
        if (history.Count == 0)
        {
            _logger.LogDebug("No history provided, returning original query");
            return query;
        }

        _logger.LogInformation(
            "Rewriting query with {HistoryCount} history messages", history.Count);

        // Build conversation history text
        var historyText = string.Join("\n",
            history.Select(m => $"{(m.IsFromAi ? "AI" : "User")}: {m.Content}"));

        var systemPrompt = $"""
            You are a query rewriting assistant. Your job is to take a user's follow-up message
            and rewrite it as a standalone search query that can be used for semantic search
            against game rule documents.
            {(gameName != null ? $"The conversation is about the board game '{gameName}'." : "")}

            Rules:
            - Replace pronouns (it, they, this, that, those) with specific nouns
            - Include relevant context from the conversation history
            - Keep the rewritten query concise and focused
            - Output ONLY the rewritten query, nothing else
            - Preserve the original language of the user's message
            """;

        var userPrompt = $"""
            Conversation history:
            {historyText}

            Follow-up message: {query}

            Standalone search query:
            """;

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var completion = await _chatCompletionService
            .GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);

        var rewrittenQuery = (completion.Content ?? query).Trim();

        _logger.LogInformation(
            "Query rewritten: '{Original}' → '{Rewritten}'", query, rewrittenQuery);

        return rewrittenQuery;
    }
}
