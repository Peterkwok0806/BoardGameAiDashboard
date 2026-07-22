using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardGameAiDashboard.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Orchestrates the full RAG pipeline: embed query → vector search → prompt assembly → LLM completion.
/// Consumes SK services (IChatCompletionService, ITextEmbeddingGenerationService) via direct DI injection
/// through Bridge Registration in DependencyInjection.cs.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorSearchService vectorSearchService,
        IChatCompletionService chatCompletionService,
        ITextEmbeddingGenerationService embeddingService,
        ILogger<RagService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _chatCompletionService = chatCompletionService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RagResponse> QueryAsync(
        string userMessage,
        Guid? gameId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "RAG query: '{Message}', gameId={GameId}", userMessage, gameId);

        // 1. Generate embedding for the user query
        var queryEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(userMessage, cancellationToken: cancellationToken);

        var embeddingArray = queryEmbedding.ToArray();

        _logger.LogDebug(
            "Generated query embedding ({Dimension} dimensions)", embeddingArray.Length);

        // 2. Similarity search in Qdrant
        var searchResults = await _vectorSearchService.SearchAsync(
            queryEmbedding: embeddingArray,
            topK: 5,
            gameId: gameId,
            cancellationToken: cancellationToken);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning("No relevant context found in Qdrant for query");

            return new RagResponse(
                Reply: "I'm sorry, I couldn't find any relevant game rules to answer your question. " +
                       "Please try rephrasing your question or check if the game rules have been uploaded.",
                Sources: Array.Empty<string>());
        }

        // 3. Build context from search results
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

        // 4. Assemble the chat prompt
        var systemPrompt = @"You are an AI assistant specializing in board game rules and strategies.
Answer the user's question based ONLY on the provided context.
If the context doesn't contain enough information to answer, say so clearly.
Always cite the relevant section(s) when referencing game rules.
Respond in the same language as the user's question (支持中文、粵語/廣東話、English).";

        var userPrompt = $"""
            {contextBuilder}

            User Question: {userMessage}

            Please provide a clear and helpful answer based on the context above.
            """;

        // 5. Call LLM via directly injected IChatCompletionService
        var chatHistory = new ChatHistory(systemPrompt);
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
