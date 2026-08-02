# ADR-001: 使用 Semantic Kernel 而非直接 HTTP

## 狀態
已接受 (Accepted)

## 日期
2024-01-15

## 上下文
本專案需要整合大型語言模型 (LLM) 來提供：
1. RAG (檢索增強生成) 聊天功能
2. 查詢重寫
3. 勝率預測的自然語言解釋

專案使用 .NET 8 + Angular 19 技術棧，需要在 C# 後端中整合 LLM 能力。

## 決策

### 選項評估

#### 選項 1: 直接 HTTP 呼叫 Ollama
```csharp
// 使用 HttpClient 直接呼叫
using var client = new HttpClient();
var response = await client.PostAsync("http://localhost:11434/api/chat",
    new StringContent(jsonPayload));

// 優點：
// - 簡單直接，無額外依賴
// - 完全控制請求/回應格式

// 缺點：
// - 需要手動管理連線、逾時、重試
// - 無 prompt 模板管理
// - 難以更換底層 LLM 供應商
// - 無結構化輸出解析
```

#### 選項 2: Semantic Kernel ⭐ 選擇
```csharp
// 使用 Semantic Kernel
var kernel = Kernel.CreateBuilder()
    .AddOllamaChatCompletion("llama3", new Uri("http://localhost:11434"))
    .Build();

var result = await kernel.InvokePromptAsync("Explain the game strategy...");

// 優點：
// - 統一的 LLM 抽象介面
// - 內建 prompt 模板管理
// - 可輕鬆更換 LLM 供應商
// - 內建 function calling 支援
// - 與 DI 深度整合

// 缺點：
// - 額外依賴
// - 學習曲線
```

#### 選項 3: LangChain.NET
```csharp
// 缺點：
// - 生態系不穩定
// - 與 .NET 整合較少
// - 社群支援不足
```

## 決策結果

**選擇：Semantic Kernel**

## 理由

### 1. 供應商無關性
Semantic Kernel 提供抽象層，可以輕鬆在不同 LLM 供應商之間切換：
- Ollama (本地)
- Azure OpenAI
- OpenAI
- Anthropic

```csharp
// 只需更改配置即可切換供應商
services.AddOpenAIChatCompletion(modelId, apiKey);
services.AddOllamaChatCompletion(modelId, endpoint);
```

### 2. Prompt 管理
```csharp
// Semantic Kernel 的 prompt 模板
var prompt = """
    <message role="system">You are a game strategy expert.</message>
    <message role="user">{{$gameHistory}}</message>
    """;

var result = await kernel.InvokePromptAsync(prompt, 
    new KernelArguments { ["gameHistory"] = history });
```

### 3. 依賴注入整合
```csharp
// Infrastructure/DependencyInjection.cs
services.AddSingleton<Kernel>(sp =>
{
    var builder = Kernel.CreateBuilder();
    builder.Services.AddOllamaChatCompletion(
        settings.ChatModel, 
        new Uri(settings.Endpoint));
    return builder.Build();
});

// Bridge: 從 Kernel 提取服務
services.AddSingleton(sp => 
    sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());
```

### 4. 與本專案架構契合
- 使用 Bridge 模式解決 ServiceProvider 隔離問題
- 可註冊為 DI singleton
- 符合 Clean Architecture 的依賴反轉原則

## 後續影響

### 正面影響
- 更容易測試（可 mock LLM）
- 可快速切換到雲端 LLM（Azure OpenAI）以提升效能
- 統一的日誌和錯誤處理

### 需注意的事項
1. Semantic Kernel 有自己的 ServiceProvider，需要 Bridge 模式與主 DI 容器整合
2. Ollama 必須在本地運行，效能取決於硬體

## 實作摘要

```csharp
// 1. 安裝套件
dotnet add package Microsoft.SemanticKernel

// 2. 配置 DI
services.AddSingleton<Kernel>(sp =>
{
    var ollamaSettings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
    var builder = Kernel.CreateBuilder();
    builder.Services.AddOllamaChatCompletion(
        ollamaSettings.ChatModel, 
        new Uri(ollamaSettings.Endpoint));
    builder.Services.AddOllamaTextEmbeddingGeneration(
        ollamaSettings.EmbeddingModel,
        new Uri(ollamaSettings.Endpoint));
    return builder.Build();
});

// 3. 使用
public class RagService
{
    private readonly Kernel _kernel;
    
    public async Task<string> QueryAsync(string question)
    {
        var result = await _kernel.InvokePromptAsync(
            $"Answer based on context: {context}\n\nQuestion: {question}");
        return result.ToString();
    }
}
```

---

## 相關檔案

- [RagService.cs](BoardGameAiDashboard/BoardGameAiDashboard.Infrastructure/Services/RagService.cs)
- [LlmQueryRewriter.cs](BoardGameAiDashboard/BoardGameAiDashboard.Infrastructure/Services/LlmQueryRewriter.cs)
- [DependencyInjection.cs](BoardGameAiDashboard/BoardGameAiDashboard.Infrastructure/DependencyInjection.cs)
- [RAG Pipeline Skill](../skills/rag-pipeline.md)
