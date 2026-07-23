using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Features.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Three-stage RAG pipeline:
/// Stage 1 — Query Rewrite: use LLM to convert follow-up into standalone query
/// Stage 2 — Retrieve: embed rewritten query → Qdrant vector search
/// Stage 3 — Generate: assemble answer with full conversation history + retrieved context
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IQueryRewriter _queryRewriter;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorSearchService vectorSearchService,
        IChatCompletionService chatCompletionService,
        ITextEmbeddingGenerationService embeddingService,
        IQueryRewriter queryRewriter,
        ILogger<RagService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _chatCompletionService = chatCompletionService;
        _embeddingService = embeddingService;
        _queryRewriter = queryRewriter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RagResponse> QueryAsync(
        string userMessage,
        Guid? gameId,
        CancellationToken cancellationToken)
    {
        // Simpler overload without history — no query rewrite
        _logger.LogInformation(
            "RAG query (no history): '{Message}', gameId={GameId}", userMessage, gameId);

        var searchResults = await VectorSearchAsync(userMessage, gameId, cancellationToken);
        return await GenerateAnswerAsync(userMessage, searchResults, history: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RagResponse> QueryAsync(
        string question,
        Guid? gameId,
        string? gameName,
        IReadOnlyList<ChatMessageDto> history,
        CancellationToken cancellationToken)
    {
        // ── Stage 1: Query Rewrite ──
        var rewrittenQuery = await _queryRewriter.RewriteAsync(
            question, history, gameName, cancellationToken);

        _logger.LogInformation(
            "RAG pipeline: rewritten query='{Rewritten}', gameId={GameId}",
            rewrittenQuery, gameId);

        // ── Stage 2: Retrieve (embed + vector search with rewritten query) ──
        var searchResults = await VectorSearchAsync(rewrittenQuery, gameId, cancellationToken);

        // ── Stage 3: Generate (answer with history + context) ──
        return await GenerateAnswerAsync(question, searchResults, history, cancellationToken);
    }

    private async Task<IReadOnlyList<VectorSearchResult>> VectorSearchAsync(
        string query, Guid? gameId, CancellationToken cancellationToken)
    {
        var queryEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);

        var embeddingArray = queryEmbedding.ToArray();

        _logger.LogDebug(
            "Generated query embedding ({Dimension} dimensions)", embeddingArray.Length);

        var searchResults = await _vectorSearchService.SearchAsync(
            queryEmbedding: embeddingArray,
            topK: 5,
            gameId: gameId,
            cancellationToken: cancellationToken);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning("No relevant context found in Qdrant for query");
        }

        return searchResults;
    }

    private async Task<RagResponse> GenerateAnswerAsync(
        string originalQuestion,
        IReadOnlyList<VectorSearchResult> searchResults,
        IReadOnlyList<ChatMessageDto>? history,
        CancellationToken cancellationToken)
    {
        if (searchResults.Count == 0)
        {
            return new RagResponse(
                Reply: "I'm sorry, I couldn't find any relevant game rules to answer your question. " +
                       "Please try rephrasing your question or check if the game rules have been uploaded.",
                Sources: Array.Empty<string>());
        }

        // Build context from search results
        var contextBuilder = new System.Text.StringBuilder();
        var sources = new List<string>();

        contextBuilder.AppendLine("=== Relevant Game Rules Context ===");
        contextBuilder.AppendLine();

        for (int i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            contextBuilder.AppendLine($"[{i + 1}] Section: {result.SectionTitle}");
            contextBuilder.AppendLine(result.Content);
            contextBuilder.AppendLine();

            sources.Add(result.SectionTitle);
        }

        contextBuilder.AppendLine("=== End of Context ===");

        // Build system prompt
        var systemPrompt = @"You are an AI assistant specializing in board game rules and strategies.
Answer the user's question based ONLY on the provided context.
If the context doesn't contain enough information to answer, say so clearly.
Always cite the relevant section(s) when referencing game rules.
Respond in the same language as the user's question (支持中文、粵語/廣東話、English).";

        // Assemble chat history
        var chatHistory = new ChatHistory(systemPrompt);

        // Add conversation history for context-aware answers
        if (history != null && history.Count > 0)
        {
            chatHistory.AddSystemMessage(
                "Below is the conversation history. Use it to understand the user's context " +
                "and provide a more relevant answer.");

            foreach (var msg in history)
            {
                if (msg.IsFromAi)
                    chatHistory.AddAssistantMessage(msg.Content);
                else
                    chatHistory.AddUserMessage(msg.Content);
            }
        }

        // Add current question with context
        var userPrompt = $"""
            {contextBuilder}

            User Question: {originalQuestion}

            Please provide a clear and helpful answer based on the context above.
            """;

        chatHistory.AddUserMessage(userPrompt);

        var completion = await _chatCompletionService
            .GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);

        var answer = completion.Content ?? string.Empty;

        _logger.LogInformation(
            "RAG response generated ({AnswerLength} chars, {SourceCount} sources)",
            answer.Length, sources.Count);

        return new RagResponse(
            Reply: answer,
            Sources: sources.Distinct().ToList());
    }
}
