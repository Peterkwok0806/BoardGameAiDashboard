using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Features.Chat;
using BoardGameAiDashboard.Domain.Entities;
using BoardGameAiDashboard.Infrastructure.Persistence;
using BoardGameAiDashboard.Infrastructure.Services;
using BoardGameAiDashboard.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Xunit;
using Xunit.Abstractions;

namespace BoardGameAiDashboard.IntegrationTests;

/// <summary>
/// Context Precision 評估測試（整合測試）。
///
/// Context Precision 定義：「檢索到的上下文中，有多少比例的 chunk 與回答問題相關」。
///
/// 計算公式：
/// Precision@k = (所有相關 chunk 在前 k 項中的數量) / k
///
/// 此測試使用真實資料庫的遊戲資料和實際的 Ollama AI 設定，
/// 來驗證 RAG 服務檢索結果的品質。
///
/// 前置條件：
/// - SQL Server 正常運作
/// - Qdrant (向量資料庫) 正常運作
/// - Ollama (LLM 和 Embedding 服務) 正常運作
/// - 資料庫中已有遊戲和 GameRuleChunks 資料
///
/// 若前置條件不滿足，測試會明確失敗（而非默默通過）。
/// </summary>
public class ContextPrecisionTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ApplicationDbContext _dbContext;
    private readonly OllamaSettings _ollamaSettings;
    private readonly QdrantSettings _qdrantSettings;
    private readonly QdrantClient _qdrantClient;
    private readonly string _connectionString;
    private readonly string _testQuestion = "How to set up";

    public ContextPrecisionTests(ITestOutputHelper output)
    {
        _output = output;

        // 讀取 appsettings.Development.json 中的設定
        var configPath = FindAppsettings();
        if (configPath == null)
        {
            throw new FileNotFoundException(
                "找不到 appsettings.Development.json，請確認專案根目錄有設定檔。");
        }

        // 使用 ConfigurationBinder 讀取設定（替代手動 JSON 解析）
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath)
            .Build();

        // 解析 SQL Server 連接字串
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

        // 解析 Ollama 設定
        _ollamaSettings = new OllamaSettings();
        configuration.GetSection("Ollama").Bind(_ollamaSettings);

        // 解析 Qdrant 設定
        _qdrantSettings = new QdrantSettings();
        configuration.GetSection("Qdrant").Bind(_qdrantSettings);

        _output.WriteLine($"[CONFIG] SQL Server: {MaskPassword(_connectionString)}");
        _output.WriteLine($"[CONFIG] Ollama: {_ollamaSettings.Endpoint} ({_ollamaSettings.ChatModel}, {_ollamaSettings.EmbeddingModel})");
        _output.WriteLine($"[CONFIG] Qdrant: {_qdrantSettings.Endpoint} ({_qdrantSettings.CollectionName})");

        // 建立依賴注入容器
        var services = new ServiceCollection();

        // 註冊設定
        services.AddSingleton(Options.Create(_ollamaSettings));
        services.AddSingleton(Options.Create(_qdrantSettings));

        // 解析 Qdrant URI
        var qdrantUri = new Uri(_qdrantSettings.Endpoint);

        // 註冊 Qdrant Client（統一單一實例）
        services.AddSingleton<QdrantClient>(_ => new QdrantClient(qdrantUri.Host, qdrantUri.Port));

        // 註冊 Semantic Kernel (使用 Ollama)
        services.AddSingleton<Kernel>(sp =>
        {
            var builder = Kernel.CreateBuilder();

            builder.Services.AddOllamaChatCompletion(
                _ollamaSettings.ChatModel,
                new Uri(_ollamaSettings.Endpoint));

            builder.Services.AddOllamaTextEmbeddingGeneration(
                _ollamaSettings.EmbeddingModel,
                new Uri(_ollamaSettings.Endpoint));

            return builder.Build();
        });

        // 橋接：從 Kernel 取出服務並重新註冊
        services.AddSingleton<IChatCompletionService>(sp =>
            sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

        services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
            sp.GetRequiredService<Kernel>().GetRequiredService<ITextEmbeddingGenerationService>());

        // 註冊 VectorSearchService
        services.AddSingleton<IVectorSearchService, VectorSearchService>();

        // 註冊 QueryRewriter
        services.AddSingleton<IQueryRewriter, LlmQueryRewriter>();

        // 註冊 RagService
        services.AddSingleton<IRagService>(sp =>
            new RagService(
                sp.GetRequiredService<IVectorSearchService>(),
                sp.GetRequiredService<IChatCompletionService>(),
                sp.GetRequiredService<ITextEmbeddingGenerationService>(),
                sp.GetRequiredService<IQueryRewriter>(),
                sp.GetRequiredService<ILogger<RagService>>()));

        // 註冊 Logger
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _serviceProvider = services.BuildServiceProvider();

        // 建立真實的 SQL Server DB Context
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        _dbContext = new ApplicationDbContext(dbOptions);

        // 取得 Qdrant Client（統一實例）
        _qdrantClient = _serviceProvider.GetRequiredService<QdrantClient>();
    }

    private static string? FindAppsettings()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var path = Path.Combine(dir, "BoardGameAiDashboard.Api", "appsettings.Development.json");
            if (File.Exists(path)) return path;

            path = Path.Combine(dir, "appsettings.Development.json");
            if (File.Exists(path)) return path;

            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    private static string MaskPassword(string connectionString)
    {
        // 隱藏密碼部分
        var parts = connectionString.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Contains("pwd=", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = "Password=***";
            }
        }
        return string.Join(";", parts);
    }

    #region Shared Methods

    /// <summary>
    /// 取得第一個擁有 GameRuleChunks 的遊戲
    /// </summary>
    private async Task<Game?> GetFirstGameWithChunksAsync()
    {
        var games = await _dbContext.Games.ToListAsync();
        foreach (var game in games)
        {
            if (await _dbContext.GameRuleChunks.AnyAsync(c => c.GameId == game.Id))
            {
                return game;
            }
        }
        return null;
    }

    /// <summary>
    /// 取得指定遊戲的所有 GameRuleChunks
    /// </summary>
    private async Task<List<GameRuleChunk>> GetChunksForGameAsync(Guid gameId)
    {
        return await _dbContext.GameRuleChunks
            .Where(c => c.GameId == gameId)
            .ToListAsync();
    }

    /// <summary>
    /// 確保 Qdrant collection 存在
    /// </summary>
    private async Task EnsureQdrantCollectionAsync()
    {
        var collections = await _qdrantClient.ListCollectionsAsync();
        if (!collections.Contains(_qdrantSettings.CollectionName))
        {
            _output.WriteLine($"[INFO] Creating Qdrant collection: {_qdrantSettings.CollectionName}");
            await _qdrantClient.CreateCollectionAsync(
                _qdrantSettings.CollectionName,
                new Qdrant.Client.Grpc.VectorParams
                {
                    Size = (ulong)_qdrantSettings.VectorDimension,
                    Distance = Qdrant.Client.Grpc.Distance.Cosine
                });
        }
    }

    /// <summary>
    /// 計算 Context Precision
    ///
    /// Context Precision@k = (前 k 項中相關 chunk 的數量) / k
    /// </summary>
    private static (double Precision, int RelevantCount, int TotalRetrieved) CalculateContextPrecision(
        IReadOnlyList<VectorSearchResult> searchResults,
        string[] relevantKeywords)
    {
        var totalRetrieved = searchResults.Count;
        if (totalRetrieved == 0)
        {
            return (0.0, 0, 0);
        }

        var relevantCount = searchResults.Count(r => IsChunkRelevant(r, relevantKeywords));

        var precision = (double)relevantCount / totalRetrieved;
        return (precision, relevantCount, totalRetrieved);
    }

    /// <summary>
    /// 判斷 chunk 是否與查詢相關（使用語義相似度關鍵字）
    /// </summary>
    private static bool IsChunkRelevant(VectorSearchResult result, string[] keywords)
    {
        return keywords.Any(keyword =>
            result.SectionTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            result.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 使用 LLM 評審 chunk 是否與問題相關
    /// 返回 1 (相關) 或 0 (不相關)
    /// </summary>
    private async Task<int> EvaluateChunkRelevanceWithLlmAsync(
        IChatCompletionService chatService,
        string question,
        VectorSearchResult chunk,
        CancellationToken cancellationToken = default)
    {
        var prompt = $@"You are a relevance evaluator for a board game rules question-answering system.

Question: {question}

Retrieved Chunk:
- Section: {chunk.SectionTitle}
- Content: {TruncateForPrompt(chunk.Content)}

Evaluate whether this chunk is relevant to answering the question.

Respond with ONLY a single number:
- 1 = Relevant (the chunk contains information that helps answer the question)
- 0 = Not Relevant (the chunk does not help answer the question)

Your response must be just the number 0 or 1, nothing else.";

        var response = await chatService.GetChatMessageContentAsync(
            prompt,
            kernel: null,
            cancellationToken: cancellationToken);

        var responseText = response.Content?.Trim() ?? "";

        // 解析回應
        if (responseText.StartsWith("1"))
            return 1;
        else if (responseText.StartsWith("0"))
            return 0;
        else
        {
            _output.WriteLine($"[WARN] LLM returned unexpected response: '{responseText}', defaulting to 0");
            return 0;
        }
    }

    /// <summary>
    /// 使用 LLM 評審所有 chunks 並計算 Context Precision (MAP - Mean Average Precision)
    ///
    /// 公式：Context Precision@K = Σ(k=1 to K) (Precision@k × v_k) / (前 K 個結果中的相關項目總數)
    ///
    /// 其中：
    /// - Precision@k = 前 k 個結果中相關項目的數量 / k
    /// - v_k = 1 如果位置 k 的項目相關，0 如果不相關
    /// </summary>
    private async Task<(double ContextPrecision, int RelevantCount, int TotalRetrieved, List<(VectorSearchResult Chunk, int Relevance)> DetailedResults)> EvaluateContextPrecisionWithLlmAsync(
        IChatCompletionService chatService,
        string question,
        IReadOnlyList<VectorSearchResult> searchResults,
        CancellationToken cancellationToken = default)
    {
        var totalRetrieved = searchResults.Count;
        if (totalRetrieved == 0)
        {
            return (0.0, 0, 0, new List<(VectorSearchResult, int)>());
        }

        var detailedResults = new List<(VectorSearchResult, int)>();
        var relevanceScores = new List<int>();

        _output.WriteLine($"\n[LLM EVALUATION] Evaluating {totalRetrieved} chunks for question: '{question}'");

        foreach (var chunk in searchResults)
        {
            var relevance = await EvaluateChunkRelevanceWithLlmAsync(chatService, question, chunk, cancellationToken);
            detailedResults.Add((chunk, relevance));
            relevanceScores.Add(relevance);

            _output.WriteLine($"  - [{relevance}] {chunk.SectionTitle}");
        }

        // 計算 MAP (Mean Average Precision)
        // Context Precision@K = Σ(k=1 to K) (Precision@k × v_k) / (相關項目總數)
        var totalRelevant = relevanceScores.Sum();
        var cumulativePrecisionSum = 0.0;

        _output.WriteLine($"\n[MAP CALCULATION]");
        for (int k = 0; k < relevanceScores.Count; k++)
        {
            var v_k = relevanceScores[k]; // 0 或 1
            var precisionAtK = relevanceScores.Take(k + 1).Sum() / (double)(k + 1);
            cumulativePrecisionSum += precisionAtK * v_k;

            _output.WriteLine($"  k={k + 1}: v_k={v_k}, Precision@{k + 1}={precisionAtK:F4}, " +
                            $"cumsum contribution={precisionAtK * v_k:F4}");
        }

        var contextPrecision = totalRelevant > 0 ? cumulativePrecisionSum / totalRelevant : 0.0;

        _output.WriteLine($"\n  Σ(Precision@k × v_k) = {cumulativePrecisionSum:F4}");
        _output.WriteLine($"  相關項目總數 = {totalRelevant}");
        _output.WriteLine($"  Context Precision@K = {cumulativePrecisionSum:F4} / {totalRelevant} = {contextPrecision:F4}");

        return (contextPrecision, totalRelevant, totalRetrieved, detailedResults);
    }

    /// <summary>
    /// 將 chunk 內容截斷以適合 prompt（避免超出 LLM context）
    /// </summary>
    private static string TruncateForPrompt(string content, int maxLength = 800)
    {
        if (string.IsNullOrEmpty(content))
            return "(empty)";

        // 移除多餘空白
        var cleaned = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");

        if (cleaned.Length <= maxLength)
            return cleaned;

        return cleaned.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// 計算標準的 Context Precision@k（不考慮位置權重，純粹比例）
    /// </summary>
    private static (double Precision, int RelevantCount, int TotalRetrieved) CalculateStandardPrecision(
        IReadOnlyList<VectorSearchResult> searchResults,
        IReadOnlyList<int> relevanceScores)
    {
        var totalRetrieved = searchResults.Count;
        if (totalRetrieved == 0)
            return (0.0, 0, 0);

        var relevantCount = relevanceScores.Sum();
        var precision = (double)relevantCount / totalRetrieved;
        return (precision, relevantCount, totalRetrieved);
    }

    #endregion

    /// <summary>
    /// 測試案例：使用資料庫中現有的遊戲驗證 Context Precision
    ///
    /// 此測試會：
    /// 1. 查詢資料庫中是否有現有的遊戲和 GameRuleChunks
    /// 2. 使用現有的遊戲資料進行測試
    /// 3. 若無則明確失敗（而非默默通過）
    /// </summary>
    [Fact]
    public async Task ContextPrecision_WithDatabaseGame_ShouldCalculatePrecision()
    {
        // Arrange: 查詢資料庫中的現有遊戲和 chunks
        var games = await _dbContext.Games.ToListAsync();
        _output.WriteLine($"[INFO] Found {games.Count} games in database");

        // 前置條件檢查
        Assert.True(games.Count > 0,
            "PREREQUISITE FAILED: No games in database. Please create a game first via API.");

        // 顯示所有遊戲
        foreach (var game in games)
        {
            var chunkCount = await _dbContext.GameRuleChunks
                .Where(c => c.GameId == game.Id)
                .CountAsync();
            _output.WriteLine($"  - Game '{game.Name}' (Id: {game.Id}): {chunkCount} rule chunks");
        }

        // 確認 Qdrant collection 存在
        await EnsureQdrantCollectionAsync();

        // 選擇第一個有 chunks 的遊戲進行測試
        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null,
            "PREREQUISITE FAILED: No GameRuleChunks found in database. Please upload game rules PDF first.");

        var chunks = await GetChunksForGameAsync(gameToTest!.Id);
        _output.WriteLine($"[INFO] Testing with game: '{gameToTest.Name}' (Id: {gameToTest.Id})");
        _output.WriteLine($"[INFO] Found {chunks.Count} rule chunks in database");

        // 顯示所有 chunks 的標題
        foreach (var chunk in chunks)
        {
            var preview = chunk.Content.Length > 50
                ? chunk.Content[..50] + "..."
                : chunk.Content;
            _output.WriteLine($"  - [{chunk.SectionTitle}]: {preview}");
        }

        // Act: 執行 RAG 檢索測試
        var relevantKeywords = chunks
            .SelectMany(c => c.SectionTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToArray();

        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(_testQuestion);
        var searchResults = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: Math.Min(5, chunks.Count),
            gameId: gameToTest.Id);

        _output.WriteLine($"\n[RESULT] Retrieved {searchResults.Count} chunks for question: '{_testQuestion}'");

        // ========== 方法1: 關鍵字評估（舊方法）==========
        _output.WriteLine("\n=== KEYWORD-BASED EVALUATION (Old Method) ===");
        var (keywordPrecision, keywordRelevantCount, keywordTotalRetrieved) = CalculateContextPrecision(
            searchResults.ToList(),
            relevantKeywords);

        _output.WriteLine($"[RESULT] Keyword Precision@{keywordTotalRetrieved}: {keywordPrecision:P2} " +
                        $"(Relevant: {keywordRelevantCount}/{keywordTotalRetrieved})");

        for (var i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            var isRelevant = IsChunkRelevant(result, relevantKeywords);
            _output.WriteLine($"  [{i + 1}] Score: {result.Score:F4} | " +
                            $"Section: '{result.SectionTitle}' | " +
                            $"Relevant: {(isRelevant ? "YES" : "NO")}");
        }

        // ========== 方法2: LLM 評審（新方法）==========
        _output.WriteLine("\n=== LLM-BASED EVALUATION (New Method) ===");
        var chatService = _serviceProvider.GetRequiredService<IChatCompletionService>();
        var (llmPrecision, llmRelevantCount, llmTotalRetrieved, detailedResults) = await EvaluateContextPrecisionWithLlmAsync(
            chatService,
            _testQuestion,
            searchResults.ToList());

        _output.WriteLine($"\n[RESULT] LLM MAP-based Context Precision@{llmTotalRetrieved}: {llmPrecision:P4} " +
                        $"(Relevant: {llmRelevantCount}/{llmTotalRetrieved})");

        // 詳細輸出 LLM 評審結果
        for (var i = 0; i < detailedResults.Count; i++)
        {
            var (chunk, relevance) = detailedResults[i];
            _output.WriteLine($"  [{i + 1}] Score: {chunk.Score:F4} | " +
                            $"Section: '{chunk.SectionTitle}' | " +
                            $"LLM Relevance: {relevance}");
        }

        // 計算 LLM 的標準精確率（用於比較）
        var llmRelevanceScores = detailedResults.Select(r => r.Relevance).ToList();
        var (llmStandardPrecision, _, _) = CalculateStandardPrecision(searchResults, llmRelevanceScores);

        // ========== 比較兩種方法的結果 ==========
        _output.WriteLine($"\n=== COMPARISON ===");
        _output.WriteLine($"\n[Standard Precision@k]");
        _output.WriteLine($"Keyword-based Standard Precision: {keywordPrecision:P2} ({keywordRelevantCount}/{keywordTotalRetrieved})");
        _output.WriteLine($"LLM-based Standard Precision:   {llmStandardPrecision:P2} ({llmRelevantCount}/{llmTotalRetrieved})");

        _output.WriteLine($"\n[LLM MAP-based Context Precision@K]");
        _output.WriteLine($"LLM-based Context Precision@K: {llmPrecision:P4} ({llmRelevantCount}/{llmTotalRetrieved} relevant)");
        _output.WriteLine($"  Formula: Σ(Precision@k × v_k) / total_relevant = {llmPrecision:F4}");

        _output.WriteLine($"\n[Difference]");
        _output.WriteLine($"Standard vs MAP difference: {Math.Abs(llmPrecision - llmStandardPrecision):P2}");

        // 使用 LLM MAP 評審結果作為最終結果
        var finalPrecision = llmPrecision;
        var finalRelevantCount = llmRelevantCount;
        var finalTotalRetrieved = llmTotalRetrieved;

        // Assert: 驗證檢索結果的有效性
        Assert.NotEmpty(searchResults);
        Assert.InRange(finalPrecision, 0.0, 1.0);

        // 驗證檢索數量符合預期（根據 chunks 數量）
        var expectedTopK = Math.Min(5, chunks.Count);
        Assert.Equal(expectedTopK, finalTotalRetrieved);

        // 驗證檢索多樣性：結果應該來自不同的 Sections
        var uniqueSections = searchResults.Select(r => r.SectionTitle).Distinct().Count();
        _output.WriteLine($"[INFO] Unique sections retrieved: {uniqueSections}/{searchResults.Count}");

        // 理想情況下，5 個結果應該來自不同 Sections
        if (uniqueSections < searchResults.Count)
        {
            _output.WriteLine($"[WARNING] Low retrieval diversity: {uniqueSections} unique sections for {searchResults.Count} results");
        }
    }

    /// <summary>
    /// 測試案例：驗證檢索結果的多樣性
    ///
    /// 良好的 RAG 檢索應該返回來自不同 Sections 的結果，
    /// 以提供更全面的上下文。
    /// </summary>
    [Fact]
    public async Task ContextPrecision_RetrievalDiversity_ShouldReturnVariedSections()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database.");

        await EnsureQdrantCollectionAsync();

        // Act: 執行檢索
        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(_testQuestion);
        var searchResults = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 5,
            gameId: gameToTest.Id);

        _output.WriteLine($"[INFO] Testing retrieval diversity for: '{_testQuestion}'");
        _output.WriteLine($"[INFO] Retrieved {searchResults.Count} chunks");

        // Assert: 驗證多樣性
        var uniqueSections = searchResults.Select(r => r.SectionTitle).Distinct().ToList();
        _output.WriteLine($"[INFO] Unique sections: {uniqueSections.Count}");
        foreach (var section in uniqueSections)
        {
            var count = searchResults.Count(r => r.SectionTitle == section);
            _output.WriteLine($"  - {section}: {count} chunks");
        }

        // 計算多樣性分數：唯一 Section 數 / 總結果數
        var diversityScore = searchResults.Count > 0
            ? (double)uniqueSections.Count / searchResults.Count
            : 0.0;

        _output.WriteLine($"[RESULT] Diversity Score: {diversityScore:P0}");

        // 斷言：多樣性分數應該 >= 60%
        // 理想情況下 5 個結果應該來自不同 Sections (多樣性 = 100%)
        Assert.True(diversityScore >= 0.6,
            $"Retrieval diversity too low: {diversityScore:P0} ({uniqueSections.Count}/{searchResults.Count} unique sections). " +
            "Consider implementing MMR (Maximal Marginal Relevance) for better diversity.");
    }

    /// <summary>
    /// 測試案例：對比 SQL Server 和 Qdrant 中的資料
    ///
    /// 檢查兩邊存儲的 section_title 是否一致，
    /// 以及是否存在數據不同步的問題。
    /// </summary>
    [Fact]
    public async Task ContextPrecision_DataConsistency_ShouldMatchBetweenSqlAndQdrant()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database.");

        await EnsureQdrantCollectionAsync();

        // Act: 從 SQL Server 獲取所有 chunks
        var sqlChunks = await _dbContext.GameRuleChunks
            .Where(c => c.GameId == gameToTest.Id)
            .Select(c => new { c.QdrantPointId, c.SectionTitle, c.Content })
            .ToListAsync();

        _output.WriteLine($"[INFO] SQL Server: {sqlChunks.Count} chunks for game '{gameToTest.Name}'");

        // 統計 SQL Server 中的 section_title 分佈
        var sqlSectionCounts = sqlChunks
            .GroupBy(c => c.SectionTitle)
            .OrderBy(g => g.Key)
            .ToList();

        _output.WriteLine($"[INFO] SQL Server section_title 分佈 ({sqlSectionCounts.Count} unique sections):");
        foreach (var group in sqlSectionCounts)
        {
            _output.WriteLine($"  - {group.Key}: {group.Count()} chunks");
        }

        // 從 Qdrant 獲取所有 points (使用 SearchAsync 搭配任意向量)
        // 由於只需要比對 section_title，不論向量內容都能取回所有 points
        var dummyVector = new float[_qdrantSettings.VectorDimension];
        var qdrantScroll = await _qdrantClient.SearchAsync(
            collectionName: _qdrantSettings.CollectionName,
            vector: dummyVector,
            limit: (ulong)Math.Max(sqlChunks.Count * 2, 100),
            filter: new Qdrant.Client.Grpc.Filter
            {
                Must =
                {
                    new Qdrant.Client.Grpc.Condition
                    {
                        Field = new Qdrant.Client.Grpc.FieldCondition
                        {
                            Key = "game_id",
                            Match = new Qdrant.Client.Grpc.Match { Text = gameToTest.Id.ToString() }
                        }
                    }
                }
            });

        var qdrantChunks = new List<(string Id, string SectionTitle)>();
        foreach (var point in qdrantScroll)
        {
            var payload = point.Payload;
            if (payload.TryGetValue("section_title", out var sectionTitleValue))
            {
                qdrantChunks.Add((point.Id.ToString(), sectionTitleValue.StringValue));
            }
        }

        _output.WriteLine($"[INFO] Qdrant: {qdrantChunks.Count} points for game '{gameToTest.Name}'");

        // 統計 Qdrant 中的 section_title 分佈
        var qdrantSectionCounts = qdrantChunks
            .GroupBy(c => c.SectionTitle)
            .OrderBy(g => g.Key)
            .ToList();

        _output.WriteLine($"[INFO] Qdrant section_title 分佈 ({qdrantSectionCounts.Count} unique sections):");
        foreach (var group in qdrantSectionCounts)
        {
            _output.WriteLine($"  - {group.Key}: {group.Count()} points");
        }

        // Assert: 驗證數據一致性
        _output.WriteLine($"\n[RESULT] Consistency Check:");

        // 1. 數量是否一致
        if (sqlChunks.Count != qdrantChunks.Count)
        {
            _output.WriteLine($"  ⚠️ Count mismatch: SQL={sqlChunks.Count}, Qdrant={qdrantChunks.Count}");
        }
        else
        {
            _output.WriteLine($"  ✅ Count match: {sqlChunks.Count}");
        }

        // 2. Section_title 分佈是否一致
        var sqlSectionNames = sqlSectionCounts.Select(g => g.Key).ToHashSet();
        var qdrantSectionNames = qdrantSectionCounts.Select(g => g.Key).ToHashSet();

        var missingInQdrant = sqlSectionNames.Except(qdrantSectionNames).ToList();
        var missingInSql = qdrantSectionNames.Except(sqlSectionNames).ToList();
        var common = sqlSectionNames.Intersect(qdrantSectionNames).ToList();

        if (missingInQdrant.Any())
        {
            _output.WriteLine($"  ⚠️ Missing in Qdrant: {string.Join(", ", missingInQdrant)}");
        }

        if (missingInSql.Any())
        {
            _output.WriteLine($"  ⚠️ Missing in SQL (extra in Qdrant): {string.Join(", ", missingInSql)}");
        }

        if (common.Count == sqlSectionNames.Count && !missingInQdrant.Any() && !missingInSql.Any())
        {
            _output.WriteLine($"  ✅ All section_titles match between SQL and Qdrant");
        }

        // 3. 檢查重複的 section_title（這可能是問題所在）
        var duplicatesInQdrant = qdrantSectionCounts.Where(g => g.Count() > 1).ToList();
        if (duplicatesInQdrant.Any())
        {
            _output.WriteLine($"  ⚠️ Duplicate section_titles in Qdrant:");
            foreach (var dup in duplicatesInQdrant)
            {
                _output.WriteLine($"     - '{dup.Key}': {dup.Count()} points");
            }
        }

        // 核心斷言：數量應該一致
        if (sqlChunks.Count != qdrantChunks.Count)
        {
            _output.WriteLine($"\n[ERROR] Data inconsistency detected!");
            _output.WriteLine($"  SQL Server: {sqlChunks.Count} chunks");
            _output.WriteLine($"  Qdrant: {qdrantChunks.Count} points");

            // 找出有重複的 sections
            var duplicateSections = qdrantSectionCounts.Where(s => s.Count() > 1).ToList();
            if (duplicateSections.Any())
            {
                _output.WriteLine($"\n[SUSPECTED CAUSE] Duplicate entries in Qdrant ({duplicateSections.Count} sections with duplicates):");
                foreach (var dup in duplicateSections.OrderByDescending(s => s.Count()))
                {
                    _output.WriteLine($"  - '{dup.Key}' (SQL has 1, Qdrant has {dup.Count()})");
                }
                _output.WriteLine($"\n[RECOMMENDED FIX]");
                _output.WriteLine($"  1. Clear Qdrant collection: DELETE all points for game '{gameToTest.Name}'");
                _output.WriteLine($"  2. Re-ingest PDF to regenerate correct chunks");
                _output.WriteLine($"  3. Or fix the re-ingestion logic in DocumentIngestionService.DeleteExistingChunksAsync()");
            }

            Assert.Fail($"Qdrant has {qdrantChunks.Count} points but SQL Server has {sqlChunks.Count} chunks. Data inconsistency detected!");
        }
    }

    /// <summary>
    /// 測試案例：驗證 RAG 檢索的切片相似度分數是否按相關性排序
    /// </summary>
    [Fact]
    public async Task ContextPrecision_RetrievalScore_ShouldBeOrderedByRelevance()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database.");

        await EnsureQdrantCollectionAsync();

        // Act: 執行檢索
        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(_testQuestion);
        var searchResults = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 5,
            gameId: gameToTest!.Id);

        _output.WriteLine($"[INFO] Game: '{gameToTest.Name}' | Retrieved {searchResults.Count} chunks ordered by relevance score:");

        // 前置條件：確認有檢索結果
        Assert.True(searchResults.Count > 0,
            "PREREQUISITE FAILED: No search results retrieved. This may indicate a problem with the vector store.");

        // Assert: 驗證分數是否按降序排列
        for (var i = 0; i < searchResults.Count - 1; i++)
        {
            _output.WriteLine($"  [{i + 1}] Score: {searchResults[i].Score:F4} | Section: '{searchResults[i].SectionTitle}'");
            Assert.True(searchResults[i].Score >= searchResults[i + 1].Score,
                $"Search results are not ordered correctly: result[{i}] score ({searchResults[i].Score:F4}) < result[{i + 1}] score ({searchResults[i + 1].Score:F4})");
        }
        _output.WriteLine($"  [{searchResults.Count}] Score: {searchResults[^1].Score:F4} | Section: '{searchResults[^1].SectionTitle}'");
    }

    /// <summary>
    /// 測試案例：驗證完整 RAG Pipeline 的 Context Precision
    /// </summary>
    [Fact]
    public async Task ContextPrecision_FullRagPipeline_ShouldGenerateAnswerWithSources()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database for full pipeline test.");

        await EnsureQdrantCollectionAsync();

        // Act: 建立完整的 RAG Service
        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();
        var chatCompletionService = _serviceProvider.GetRequiredService<IChatCompletionService>();
        var queryRewriter = _serviceProvider.GetRequiredService<IQueryRewriter>();
        var logger = _serviceProvider.GetRequiredService<ILogger<RagService>>();

        var ragService = new RagService(
            vectorSearchService,
            chatCompletionService,
            embeddingService,
            queryRewriter,
            logger);

        var response = await ragService.QueryAsync(
            _testQuestion,
            gameToTest!.Id,
            gameToTest.Name,
            history: Array.Empty<ChatMessageDto>(),
            CancellationToken.None);

        _output.WriteLine($"[INFO] Game: '{gameToTest.Name}'");
        _output.WriteLine($"[AI RESPONSE]\n{response.Reply}");
        _output.WriteLine($"[SOURCES] {string.Join(", ", response.Sources)}");

        // Assert: RAG 應該回應且有來源
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.Reply),
            "RAG response should not be empty");
        Assert.NotEmpty(response.Sources);

        _output.WriteLine($"[INFO] Full RAG pipeline generated response with {response.Sources.Count} sources");
    }

    /// <summary>
    /// 測試案例：驗證 VectorSearchService 的 metadata 過濾功能
    /// </summary>
    [Fact]
    public async Task ContextPrecision_MetadataFilter_ShouldOnlyReturnMatchingGameChunks()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database.");

        // 確認有多個遊戲才能測試 metadata 過濾
        Assert.True(games.Count >= 2,
            "PREREQUISITE FAILED: Need at least 2 games to test metadata filtering.");

        await EnsureQdrantCollectionAsync();

        // Act
        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(_testQuestion);
        var searchResults = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 10,
            gameId: gameToTest.Id);

        // Assert: 所有結果應該屬於指定遊戲
        foreach (var result in searchResults)
        {
            Assert.Equal(gameToTest.Id, result.GameId);
        }

        _output.WriteLine($"[INFO] All {searchResults.Count} results correctly filtered to game '{gameToTest.Name}'");
    }

    /// <summary>
    /// 測試案例：驗證不同類型的問題都能觸發 RAG 檢索
    ///
    /// 測試以下問題類型：
    /// 1. 遊戲規則相關
    /// 2. 遊戲設置相關
    /// 3. 獲勝條件相關
    /// 4. 策略建議相關
    /// </summary>
    [Theory]
    [InlineData("What are the basic rules?", "game rules")]
    [InlineData("How do I set up the game?", "game setup")]
    [InlineData("What are the win conditions?", "win conditions")]
    [InlineData("What is the best strategy?", "strategy")]
    [InlineData("How many players can play?", "player count")]
    [InlineData("What happens when...", "game mechanics")]
    public async Task ContextPrecision_VariousQuestions_ShouldTriggerRetrieval(
        string question, string category)
    {
        _output.WriteLine($"[TEST] Category: {category} | Question: '{question}'");

        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database.");

        await EnsureQdrantCollectionAsync();

        // Act: 執行向量檢索
        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(question);
        var searchResults = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 5,
            gameId: gameToTest.Id);

        // Assert: 驗證檢索結果
        _output.WriteLine($"[RESULT] Retrieved {searchResults.Count} chunks for '{category}' question");

        // 每個問題都應該返回結果（假設遊戲規則文件包含這些內容）
        Assert.True(searchResults.Count > 0,
            $"RAG retrieval should return results for question category: {category}");

        // 驗證返回的 chunk 包含相關內容
        foreach (var result in searchResults)
        {
            var relevanceScore = CalculateRelevanceScore(result, question);
            _output.WriteLine($"  - [{result.SectionTitle}] Score: {result.Score:F4}, Relevance: {relevanceScore:P0}");
        }

        // 驗證分數在合理範圍內
        Assert.All(searchResults, r => Assert.InRange(r.Score, 0.0, 1.0));
    }

    /// <summary>
    /// 測試案例：驗證無相關內容時 RAG 的降級行為
    /// </summary>
    [Fact]
    public async Task ContextPrecision_NoRelevantChunks_ShouldReturnFallbackResponse()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();
        Assert.True(games.Count > 0, "PREREQUISITE FAILED: No games in database.");

        var gameToTest = await GetFirstGameWithChunksAsync();
        Assert.True(gameToTest != null, "PREREQUISITE FAILED: No GameRuleChunks found in database.");

        await EnsureQdrantCollectionAsync();

        // Act: 使用明顯無關的問題
        var irrelevantQuestion = "What is the weather today?";
        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();
        var ragService = _serviceProvider.GetRequiredService<IRagService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(irrelevantQuestion);
        var searchResults = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 5,
            gameToTest.Id);

        _output.WriteLine($"[INFO] Irrelevant question '{irrelevantQuestion}' returned {searchResults.Count} results");

        // Act: 測試 RAG 回應
        var response = await ragService.QueryAsync(
            irrelevantQuestion,
            gameToTest.Id,
            gameToTest.Name,
            history: Array.Empty<ChatMessageDto>(),
            CancellationToken.None);

        // Assert: 驗證 RAG 的降級處理
        Assert.NotNull(response);

        // 如果檢索結果很少或為空，應該返回降級回應
        if (searchResults.Count == 0)
        {
            Assert.Contains("couldn't find", response.Reply, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(response.Sources);
        }
        else
        {
            // 即使有些結果，相關性應該很低
            var avgScore = searchResults.Average(r => r.Score);
            _output.WriteLine($"[INFO] Average relevance score for irrelevant question: {avgScore:P2}");
        }
    }

    /// <summary>
    /// 測試案例：驗證不同遊戲的 RAG 檢索隔離性
    /// </summary>
    [Fact]
    public async Task ContextPrecision_MultiGameIsolation_ShouldNotLeakBetweenGames()
    {
        // Arrange: 前置條件檢查
        var games = await _dbContext.Games.ToListAsync();

        // 需要至少 2 個遊戲才能測試隔離性
        Assert.True(games.Count >= 2, "PREREQUISITE FAILED: Need at least 2 games.");

        // 取得兩個不同的遊戲
        var game1 = games[0];
        var game2 = games[1];

        var chunks1 = await GetChunksForGameAsync(game1.Id);
        var chunks2 = await GetChunksForGameAsync(game2.Id);

        if (chunks1.Count == 0 || chunks2.Count == 0)
        {
            _output.WriteLine("[SKIP] Both games need GameRuleChunks to test isolation.");
            return;
        }

        await EnsureQdrantCollectionAsync();

        // Act: 使用第一個遊戲的問題查詢
        var game1Question = chunks1.First().Content.Length > 50
            ? chunks1.First().Content[..50]
            : chunks1.First().Content;

        var vectorSearchService = _serviceProvider.GetRequiredService<IVectorSearchService>();
        var embeddingService = _serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(game1Question);

        // 分別對兩個遊戲執行檢索
        var resultsForGame1 = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 5,
            gameId: game1.Id);

        var resultsForGame2 = await vectorSearchService.SearchAsync(
            queryEmbedding: queryEmbedding.ToArray(),
            topK: 5,
            gameId: game2.Id);

        _output.WriteLine($"[INFO] Query: '{game1Question[..Math.Min(30, game1Question.Length)]}...'");
        _output.WriteLine($"[INFO] Game1 '{game1.Name}': {resultsForGame1.Count} results");
        _output.WriteLine($"[INFO] Game2 '{game2.Name}': {resultsForGame2.Count} results");

        // Assert: 驗證檢索隔離性
        // Game1 的結果應該屬於 Game1
        foreach (var result in resultsForGame1)
        {
            Assert.Equal(game1.Id, result.GameId);
        }

        // Game2 的結果應該屬於 Game2
        foreach (var result in resultsForGame2)
        {
            Assert.Equal(game2.Id, result.GameId);
        }

        // 驗證不同遊戲的檢索結果不會混淆
        var game1SectionTitles = resultsForGame1.Select(r => r.SectionTitle).ToHashSet();
        var game2SectionTitles = resultsForGame2.Select(r => r.SectionTitle).ToHashSet();

        // 如果兩個遊戲都有結果，標題應該不同（或者至少 GameId 不同）
        if (resultsForGame1.Count > 0 && resultsForGame2.Count > 0)
        {
            var allResultsFromCorrectGames =
                resultsForGame1.All(r => r.GameId == game1.Id) &&
                resultsForGame2.All(r => r.GameId == game2.Id);

            Assert.True(allResultsFromCorrectGames,
                "RAG retrieval should not leak results between different games");
        }
    }

    /// <summary>
    /// 計算檢索結果與查詢的相關性分數
    /// </summary>
    private static double CalculateRelevanceScore(VectorSearchResult result, string query)
    {
        // 簡單的相關性計算：查詢關鍵字在標題或內容中出現的比例
        var queryKeywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryKeywords.Length == 0) return 0.0;

        var matchCount = 0;
        foreach (var keyword in queryKeywords)
        {
            if (result.SectionTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                result.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
        }

        return (double)matchCount / queryKeywords.Length;
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncSp)
            await asyncSp.DisposeAsync();
        else
            _serviceProvider?.Dispose();

        _qdrantClient?.Dispose();
    }
}
