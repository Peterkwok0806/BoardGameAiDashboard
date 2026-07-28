# RAG 流程技能

## 觸發時機
當使用者需要實作 RAG 功能、建置 RAG 流程、建立向量搜尋、擷取文件，或任何與 AI 聊天、嵌入（embeddings）或知識庫相關的功能時使用此技能。

## 架構概覽

本專案使用 RAG（檢索增強生成）流程，搭配：
- **Qdrant** 用於向量儲存
- **Semantic Kernel** 用於 LLM 編排
- **Ollama** 用於本地 LLM（可替換為 Azure OpenAI）

### 流程圖

```
PDF 上傳 → 文字擷取 → 語意分塊 → 嵌入生成 → Qdrant 儲存
                                                              ↓
使用者查詢 → 嵌入查詢 → Qdrant 搜尋（metadata 過濾）→ 建構 Context → LLM 回應
```

### 關鍵檔案位置

| 元件 | 檔案 | 用途 |
|------|------|------|
| 向量搜尋 | `Infrastructure/Services/VectorSearchService.cs` | Qdrant CRUD + 帶 metadata 過濾的搜尋 |
| 文件擷取 | `Infrastructure/Services/DocumentIngestionService.cs` | PDF → chunks → 嵌入 → 儲存流程 |
| RAG 協調器 | `Infrastructure/Services/RagService.cs` | 查詢重寫、Context 建構、回應生成 |
| PDF 解析器 | `Infrastructure/Services/PdfParser.cs` | PDF 文字擷取 |
| 分塊器 | `Infrastructure/Services/DocumentChunker.cs` | 語意文字分塊 |
| 介面 | `Application/Common/Interfaces/IVectorSearchService.cs` | 向量操作合約 |
| 設定 | `appsettings.json` → `Qdrant:*` | 端點、collection 名稱、維度 |

## 必要模式

### 1. Metadata 過濾（強制執行）

每個 Qdrant 搜尋**必須**依據 `game_id` 和/或 `section_title` 進行過濾：

```csharp
// ✅ 正確 — 始終包含 metadata 過濾
Filter? filter = null;
if (gameId.HasValue)
{
    filter = new Filter
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "game_id",
                    Match = new Match { Text = gameId.Value.ToString() }
                }
            }
        }
    };
}
var results = await _client.SearchAsync(
    collectionName: _settings.CollectionName,
    vector: queryEmbedding,
    limit: (ulong)topK,
    filter: filter,
    cancellationToken: cancellationToken);
```

```csharp
// ❌ 錯誤 — 絕對不要在沒有 metadata 過濾的情況下搜尋
var results = await _client.SearchAsync(collectionName, vector, limit);
```

### 2. 送 LLM 前先分塊（強制執行）

絕對不要將整份文件直接塞進 LLM context。始終：
1. 從 PDF 擷取文字
2. 分割為段落
3. 為每個段落分塊（每個 chunk 約 500 tokens）
4. 為每個 chunk 生成嵌入
5. 帶 metadata 儲存到 Qdrant

### 3. 使用 Semantic Kernel（強制執行）

所有 LLM 操作都使用 Semantic Kernel，**禁止**直接 HTTP 呼叫：

```csharp
// ✅ 正確
var kernel = sp.GetRequiredService<Kernel>();
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

// ❌ 錯誤 — 絕對不要對 LLM 使用原始 HTTP
using var client = new HttpClient();
await client.PostAsync("https://api.openai.com/...", ...);
```

### 4. Semantic Kernel DI Bridge 模式

本專案使用 bridge 模式，因為 Semantic Kernel 會建立自己的 ServiceProvider：

```csharp
// DependencyInjection.cs 中
services.AddSingleton<Kernel>(sp =>
{
    var ollamaSettings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
    var builder = Kernel.CreateBuilder();
    builder.Services.AddOllamaChatCompletion(ollamaSettings.ChatModel, new Uri(ollamaSettings.Endpoint));
    builder.Services.AddOllamaTextEmbeddingGeneration(ollamaSettings.EmbeddingModel, new Uri(ollamaSettings.Endpoint));
    return builder.Build();
});

// Bridge：從 Kernel 的內部 SP 擷取並重新註冊
services.AddSingleton(sp => sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());
services.AddSingleton(sp => sp.GetRequiredService<Kernel>().GetRequiredService<ITextEmbeddingGenerationService>());
```

## 新增 RAG 功能

1. **新介面** → `Application/Common/Interfaces/IRagService.cs` 或類似檔案
2. **實作** → `Infrastructure/Services/RagService.cs`（或新檔案）
3. **CQRS Handler** → `Application/Features/Chat/.../`
4. **Controller** → `Api/Controllers/ChatController.cs`
5. **註冊於** → `Infrastructure/DependencyInjection.cs`

## 文件擷取流程

```csharp
public async Task<int> IngestGameRulesAsync(
    Guid gameId,
    Stream pdfStream,
    IReadOnlyList<string>? sectionTitles = null,
    CancellationToken cancellationToken = default)
{
    // 1. 從 PDF 擷取文字（處理不可搜尋的串流）
    var rawText = await _pdfParser.ExtractTextAsync(streamToUse, cancellationToken);
    
    // 2. 分割為具名段落
    var sections = SegmentIntoSections(rawText, sectionTitles);
    
    // 3. 為所有段落分塊
    var chunks = _documentChunker.ChunkAll(sections, gameId);
    
    // 4. 刪除現有 chunks（重新擷取）
    await DeleteExistingChunksAsync(gameId, cancellationToken);
    
    // 5. 確保 Qdrant collection 存在
    await _vectorSearchService.EnsureCollectionAsync(cancellationToken);
    
    // 6. 處理每個 chunk：嵌入 → 儲存到 Qdrant → 儲存到 EF Core
    foreach (var chunk in chunks)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
        await _vectorSearchService.UpsertAsync(pointId, embedding, metadata, cancellationToken);
        await _unitOfWork.Rules.AddAsync(ruleChunk, cancellationToken);
    }
    
    // 7. 在一個交易中持久化所有變更
    await _unitOfWork.SaveChangesAsync(cancellationToken);
}
```

## RAG 查詢流程

```csharp
public async Task<RagResponse> QueryAsync(string question, Guid? gameId, ...)
{
    // 1. 重寫查詢（可選，用於更好的檢索）
    var rewrittenQuery = await _queryRewriter.RewriteAsync(question);
    
    // 2. 嵌入查詢
    var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(rewrittenQuery);
    
    // 3. 使用 metadata 過濾搜尋 Qdrant
    var results = await _vectorSearchService.SearchAsync(
        queryEmbedding.ToArray(), topK, gameId, cancellationToken);
    
    // 4. 從檢索到的 chunks 建構 context
    var context = BuildContext(results);
    
    // 5. 透過 Semantic Kernel 生成回應
    var response = await _kernel.InvokePromptAsync(systemPrompt + context + question);
    
    return new RagResponse(response, ExtractSources(results));
}
```

## 日誌需求

為除錯和改進記錄所有 RAG 操作：
```csharp
_logger.LogInformation("Starting PDF ingestion for GameId={GameId}", gameId);
_logger.LogDebug("Qdrant search returned {Count} results", searchResults.Count);
_logger.LogWarning("PDF extraction returned empty content for GameId={GameId}", gameId);
```

## 測試 RAG 功能

```csharp
// 單元測試範例
[Fact]
public async Task VectorSearchService_SearchAsync_FiltersbyGameId()
{
    // Arrange
    var gameId = Guid.NewGuid();
    var queryEmbedding = new float[1024]; // 符合 settings 中的 VectorDimension
    
    // Act
    var results = await _service.SearchAsync(queryEmbedding, topK: 5, gameId, ct);
    
    // Assert
    Assert.All(results, r => Assert.Equal(gameId, r.GameId));
}
```

## 錯誤處理

- PDF 擷取失敗時傳回 0 個 chunks
- 空結果時記錄警告但不拋出異常
- 只在關鍵失敗（資料庫連線、Qdrant 不可用）時拋出異常
- 使用網域異常：`ValidationException` 處理無效輸入

## 效能優化

1. 對多個 chunks 使用批次 upsert
2. 在 Redis 快取 RAG 回應（TTL: 30 分鐘）
3. 唯讀查詢使用 `AsNoTracking()`
4. 考慮對長回應使用非同步串流

## 範例

### 新增向量搜尋端點
```csharp
// 1. 介面
public interface ISemanticSearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(float[] embedding, int topK, 
        Guid? gameId, CancellationToken ct);
}

// 2. CQRS Query
public record SemanticSearchQuery(string Query, Guid? GameId) 
    : IRequest<ApiResult<IReadOnlyList<SearchResultDto>>>;

// 3. Controller
[HttpPost("search")]
public async Task<IActionResult> Search([FromBody] SemanticSearchQuery query)
{
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

### 擷取新遊戲規則
```csharp
// Controller
[HttpPost("{gameId}/rules")]
public async Task<IActionResult> UploadRules(Guid gameId, IFormFile file)
{
    await _mediator.Send(new IngestRulesCommand(gameId, file.OpenReadStream()));
    return Ok();
}
```
