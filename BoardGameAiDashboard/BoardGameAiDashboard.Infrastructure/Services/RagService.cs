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
/// Four-stage RAG pipeline with LLM Reranking:
/// Stage 1 — Query Rewrite: use LLM to convert follow-up into standalone query
/// Stage 2 — Retrieve: embed rewritten query → Qdrant vector search (top 5)
/// Stage 3 — Rerank: use LLM to evaluate relevance and select top 3 most relevant chunks
/// Stage 4 — Generate: assemble answer with filtered context
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IQueryRewriter _queryRewriter;
    private readonly ILogger<RagService> _logger;

    /// <summary>Number of chunks to retrieve initially from vector search</summary>
    private const int InitialTopK = 5;

    /// <summary>Number of chunks to keep after reranking</summary>
    private const int FinalTopK = 3;

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

        // Apply LLM reranking
        var rerankedResults = await RerankChunksAsync(userMessage, searchResults, cancellationToken);

        return await GenerateAnswerAsync(userMessage, rerankedResults, history: null, cancellationToken);
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

        // ── Stage 3: LLM Reranking ──
        var rerankedResults = await RerankChunksAsync(question, searchResults, cancellationToken);

        // ── Stage 4: Generate (answer with filtered context) ──
        return await GenerateAnswerAsync(question, rerankedResults, history, cancellationToken);
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
            topK: InitialTopK,
            gameId: gameId,
            cancellationToken: cancellationToken);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning("No relevant context found in Qdrant for query");
        }
        else
        {
            _logger.LogInformation(
                "Vector search returned {Count} chunks (will be reranked to top {TopK})",
                searchResults.Count, FinalTopK);
        }

        return searchResults;
    }

    /// <summary>
    /// Stage 3: Use LLM to evaluate relevance of each chunk and rerank them.
    /// Returns only the top K most relevant chunks.
    /// </summary>
    private async Task<IReadOnlyList<VectorSearchResult>> RerankChunksAsync(
        string question,
        IReadOnlyList<VectorSearchResult> searchResults,
        CancellationToken cancellationToken)
    {
        if (searchResults.Count == 0)
        {
            return searchResults;
        }

        _logger.LogInformation(
            "Reranking {Count} chunks using LLM for question: '{Question}'",
            searchResults.Count, question);

        var rerankingPrompt = $@"You are a relevance evaluator for a board game rules question-answering system.

Question: {question}

Evaluate each chunk's relevance to answering the question.
Respond with ONLY a JSON array of numbers (0 or 1), one per chunk in order.
- 1 = Relevant (helps answer the question)
- 0 = Not Relevant (does not help answer)

Chunks to evaluate:
{string.Join("\n", searchResults.Select((r, i) => $"Chunk {i + 1} [{r.SectionTitle}]: {TruncateForReranking(r.Content)}"))}

Example response format:
[1, 0, 1, 0, 1]

Respond with ONLY the JSON array, nothing else.";

        try
        {
            var completion = await _chatCompletionService
                .GetChatMessageContentAsync(rerankingPrompt, cancellationToken: cancellationToken);

            var responseText = completion.Content?.Trim() ?? "";

            // Parse the JSON array response
            var relevanceScores = ParseRelevanceScores(responseText, searchResults.Count);

            // Sort by relevance score (descending), then by original vector score as tiebreaker
            var reranked = searchResults
                .Select((result, index) => new { Result = result, Relevance = relevanceScores[index] })
                .Where(x => x.Relevance > 0) // Only keep relevant chunks
                .OrderByDescending(x => x.Relevance)
                .ThenByDescending(x => x.Result.Score)
                .Take(FinalTopK)
                .Select(x => x.Result)
                .ToList();

            var keptCount = reranked.Count;
            var filteredCount = searchResults.Count - keptCount;

            _logger.LogInformation(
                "Reranking complete: kept {Kept}/{Total} chunks (filtered {Filtered} irrelevant)",
                keptCount, searchResults.Count, filteredCount);

            // Log which chunks were kept
            for (int i = 0; i < reranked.Count; i++)
            {
                _logger.LogDebug(
                    "  Reranked chunk {Rank}: [{Section}] (vector score: {Score:F4})",
                    i + 1, reranked[i].SectionTitle, reranked[i].Score);
            }

            return reranked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM reranking failed, falling back to original order");

            // Fallback: return top K by vector score
            return searchResults
                .OrderByDescending(r => r.Score)
                .Take(FinalTopK)
                .ToList();
        }
    }

    /// <summary>
    /// Parse LLM response to extract relevance scores.
    /// Expected format: [1, 0, 1, 0, 1]
    /// </summary>
    private static List<int> ParseRelevanceScores(string response, int expectedCount)
    {
        var scores = new List<int>();

        try
        {
            // Try to parse as JSON array
            var trimmed = response.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                var parts = trimmed.Trim('[', ']', ' ')
                    .Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out var score))
                    {
                        scores.Add(Math.Clamp(score, 0, 1)); // Clamp to 0 or 1
                    }
                }
            }
        }
        catch
        {
            // Parsing failed
        }

        // If parsing failed or wrong count, return all 1s (optimistic)
        while (scores.Count < expectedCount)
        {
            scores.Add(1);
        }

        return scores.Take(expectedCount).ToList();
    }

    /// <summary>
    /// Truncate content for reranking prompt to avoid token limits.
    /// </summary>
    private static string TruncateForReranking(string content, int maxLength = 400)
    {
        if (string.IsNullOrEmpty(content))
            return "(empty)";

        // Clean up whitespace
        var cleaned = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");

        if (cleaned.Length <= maxLength)
            return cleaned;

        return cleaned.Substring(0, maxLength) + "...";
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

RESPONSE FORMAT (IMPORTANT):
1. Answer the question using bullet points for clarity
2. Put all sources/references at the END of your response under a 'Sources:' section
3. Format sources as a simple numbered list
4. Keep your answer concise but informative

Example format:
• First main point about the game rules
• Second main point
• Third point with important details

Sources:
1. Section Title 1
2. Section Title 2

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
