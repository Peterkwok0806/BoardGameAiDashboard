# Troubleshooting Guide

本文件收錄本專案常見問題的排查步驟和解決方案。

---

## 目錄

1. [軟刪除問題](#1-軟刪除問題)
2. [RAG 查詢問題](#2-rag-查詢問題)
3. [JWT 認證問題](#3-jwt-認證問題)
4. [EF Core 遷移問題](#4-ef-core-遷移問題)
5. [Angular 前端問題](#5-angular-前端問題)
6. [建置和執行問題](#6-建置和執行問題)

---

## 1. 軟刪除問題

### 問題：查詢返回空結果，但實體應該存在

**徵兆**：
```csharp
var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == gameId);
// 返回 null，但確定存在
```

**原因**：全域查詢過濾器自動過濾 `IsDeleted == true` 的實體

**解決方案**：

1. 檢查是否已軟刪除：
```csharp
// 使用 IgnoreQueryFilters 暫時繞過過濾器
var game = await _context.Games
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(g => g.Id == gameId);

if (game != null && game.IsDeleted)
{
    // 實體已被軟刪除
}
```

2. 確認刪除方法是否正確調用：
```csharp
// ✅ 正確：使用網域方法
game.Delete();

// ❌ 錯誤：直接設置屬性
game.IsDeleted = true; // 會繞過 UpdatedAt 更新
```

3. 檢查 DbContext 配置：
```csharp
// 確保軟刪除過濾器存在
modelBuilder.Entity<Game>(entity =>
{
    entity.HasQueryFilter(e => e.IsDeleted == false);
});
```

---

### 問題：刪除後查詢仍然返回實體

**原因**：DbContext 快取問題

**解決方案**：
```csharp
// 清除上下文快取後重新查詢
_context.ChangeTracker.Clear();
var game = await _context.Games.FindAsync(gameId);
```

---

## 2. RAG 查詢問題

### 問題：向量搜尋返回空結果

**徵兆**：
```csharp
var results = await _vectorSearchService.SearchAsync(embedding, topK: 5, gameId);
// 返回空列表
```

**排查步驟**：

#### Step 1: 檢查 Qdrant 連接
```csharp
// 確認 Qdrant 服務正在執行
// 預設端點：http://localhost:6333
```

#### Step 2: 檢查 Collection 是否存在
```csharp
// 使用 Qdrant Dashboard 或 curl
curl http://localhost:6333/collections/{CollectionName}
```

#### Step 3: 檢查嵌入維度
```csharp
// 確認 embeddings 模型維度與 Qdrant collection 設定一致
// 檢查 QdrantSettings.VectorDimension 設定
public class QdrantSettings
{
    public int VectorDimension { get; set; } = 1024; // 必須與 embedding 模型一致
}
```

#### Step 4: 檢查 Metadata 過濾
```csharp
// 確保過濾條件正確
var results = await _client.SearchAsync(
    collectionName: "game_rules",
    vector: embedding,
    limit: 5,
    filter: new Filter
    {
        Must = new List<Condition>
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "game_id",
                    Match = new Match { Text = gameId.ToString() }
                }
            }
        }
    }
);
```

---

### 問題：PDF 攧取返回空內容

**徵兆**：
```csharp
var chunks = await _documentIngestionService.IngestGameRulesAsync(gameId, pdfStream);
// 返回 0 個 chunks
```

**排查步驟**：

1. 檢查 PDF 是否為可搜尋格式：
```csharp
// 嘗試讀取文字內容
var rawText = await _pdfParser.ExtractTextAsync(stream);
// 如果 rawText 為空或只有空格，PDF 可能已加密
```

2. 檢查 PDF 是否加密：
```csharp
// 使用 iText7 檢查
using var reader = new PdfReader(stream);
if (reader.IsEncrypted())
{
    throw new ValidationException("PDF is encrypted and cannot be processed");
}
```

3. 檢查串流位置：
```csharp
// 確保串流位置在開頭
stream.Position = 0;
```

---

### 問題：LLM 回應緩慢或超時

**原因**：
1. Ollama 服務未正確配置
2. 模型未下載
3. 上下文太長

**解決方案**：

1. 確認 Ollama 服務正在執行：
```bash
ollama list  # 查看已下載模型
ollama serve # 啟動服務
```

2. 檢查連接配置：
```json
// appsettings.json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ChatModel": "llama3",  // 確認模型名稱正確
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

3. 減少上下文大小：
```csharp
// 限制 chunk 數量
const int maxChunks = 5;
var relevantChunks = chunks.Take(maxChunks);
```

---

## 3. JWT 認證問題

### 問題：收到 401 Unauthorized

**排查步驟**：

#### Step 1: 檢查 Token 是否過期
```csharp
// JWT tokens 有 15 分鐘 TTL
// 如果收到 401，可能是：
// 1. Token 已過期
// 2. Token 格式錯誤
// 3. Signature 驗證失敗
```

#### Step 2: 檢查 Header 配置
```csharp
// 確保 API 使用正確的認證配置
app.UseAuthentication();
app.UseAuthorization();
```

#### Step 3: 檢查前端 Token 附加
```typescript
// Angular interceptor 應該附加 Bearer token
// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = authService.token();
  if (token && !req.url.includes('/auth/')) {
    const authReq = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
    return next(authReq);
  }
  return next(req);
};
```

---

### 問題：Refresh Token 無效

**徵兆**：
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Unauthorized",
  "status": 401,
  "message": "Invalid refresh token"
}
```

**原因**：
1. Refresh token 已使用（單次使用）
2. Token 已過期
3. Token 已被撤銷

**解決方案**：

1. 單次使用：每次刷新後都會生成新 token，舊的失效
```csharp
// RefreshTokenCommandHandler.cs
// 舊 token 被標記為 Revoked
await _unitOfWork.RefreshTokens.RevokeAsync(oldToken);
```

2. 用戶需要重新登入獲取新 refresh token

---

### 問題：JwtTokenService 生成 Token 失敗

**徵兆**：
```
SigningCredentials creation failed: The key size must be greater than...
```

**原因**：JWT Secret 太短

**解決方案**：
```json
// appsettings.json — Secret 至少需要 32 個字元
{
  "Jwt": {
    "Secret": "YOUR_SUPER_SECRET_KEY_MUST_BE_AT_LEAST_32_CHARS!"
  }
}
```

---

## 4. EF Core 遷移問題

### 問題：遷移失敗「Unable to determine the relationship」

**原因**：導航屬性配置不正確

**解決方案**：
```csharp
// 明確設定關係
modelBuilder.Entity<GameRuleChunk>(entity =>
{
    entity.HasOne(c => c.Game)
          .WithMany(g => g.RuleChunks)
          .HasForeignKey(c => c.GameId)
          .OnDelete(DeleteBehavior.Cascade);
});
```

---

### 問題：遷移失敗「Cannot insert explicit value for identity column」

**原因**：手動指定了 ID，但資料庫設定為自動生成

**解決方案**：
```csharp
// 讓資料庫自動生成 ID
entity.Property(e => e.Id).ValueGeneratedOnAdd();
```

---

### 問題：遷移與現有資料衝突

**原因**：新欄位有 NOT NULL 約束但沒有預設值

**解決方案**：
1. 添加遷移時指定預設值：
```csharp
migrationBuilder.AddColumn<string>(
    name: "NewColumn",
    table: "Games",
    nullable: false,
    defaultValue: "default");
```

2. 或者先允許 NULL，之後遷移資料再改為 NOT NULL

---

### 問題：刪除遷移失敗

**原因**：遷移已套用到資料庫

**解決方案**：
```bash
# 回復到上一個遷移
dotnet ef database update <PreviousMigrationName> --project BoardGameAiDashboard.Infrastructure --startup-project BoardGameAiDashboard.Api

# 然後刪除遷移
dotnet ef migrations remove --project BoardGameAiDashboard.Infrastructure
```

---

## 5. Angular 前端問題

### 問題：API 請求返回 401 但用戶已登入

**排查步驟**：

1. 檢查 Token 存儲：
```typescript
// AuthService 應該正確保存 token
readonly token = signal<string | null>(localStorage.getItem('accessToken'));
```

2. 檢查 Interceptor 配置：
```typescript
// app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(
      withInterceptors([authInterceptor, apiInterceptor])
    ),
  ],
};
```

3. 檢查 Token 過期處理：
```typescript
// 應該在 401 回應時刷新 token
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // ... 處理 401 並刷新 token
};
```

---

### 問題：Signal 更新後 UI 沒有響應

**原因**：沒有使用 OnPush Change Detection 或更新方式不正確

**解決方案**：

1. 確保使用正確的 Signal 更新方式：
```typescript
// ✅ 正確
this._data.set(newValue);
this._data.update(items => [...items, newItem]);

// ❌ 錯誤
this._data().push(newItem); // 不會觸發更新
```

2. 啟用 OnPush Change Detection：
```typescript
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  // ...
})
```

---

### 問題：Component 顯示舊資料

**原因**：快取問題或 HttpClient 回應被快取

**解決方案**：

1. 使用 `take(1)` 取最新值：
```typescript
readonly games = toSignal(
  this.http.get<Game[]>('/api/games').pipe(take(1)),
  { initialValue: [] }
);
```

2. 添加 cache busting header：
```typescript
provideHttpClient(
  withInterceptors([...]),
  withFetch()
)
```

---

## 6. 建置和執行問題

### 問題：dotnet build 失敗

**排查步驟**：

1. 檢查還原依賴：
```bash
dotnet restore
dotnet build
```

2. 清除並重建：
```bash
dotnet clean
dotnet build
```

3. 刪除 bin/obj 資料夾：
```bash
find . -type d -name "bin" -exec rm -rf {} +
find . -type d -name "obj" -exec rm -rf {} +
dotnet restore
dotnet build
```

---

### 問題：Angular npm start 失敗

**排查步驟**：

1. 檢查 node_modules：
```bash
cd DashboardFrontend
rm -rf node_modules
npm install
npm start
```

2. 檢查 Angular CLI：
```bash
npx ng version
npm install -D @angular/cli@latest
```

3. 檢查埠口占用：
```bash
# Windows
netstat -ano | findstr :4200
taskkill /PID <pid> /F
```

---

### 問題：Redis 連接失敗

**徵兆**：
```
StackExchange.Redis.RedisConnectionException: It was not possible to connect...
```

**解決方案**：

1. 確認 Redis 服務正在執行：
```bash
redis-cli ping
# 應該返回 PONG
```

2. 檢查連接字串：
```json
// appsettings.json
{
  "Redis": {
    "Connection": "localhost:6379"
  }
}
```

3. 檢查連接配置：
```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "BoardGameAiDashboard:";
});
```

---

## 快速參考

### 常見錯誤代碼

| HTTP 狀態碼 | 意義 | 常見原因 |
|-------------|------|---------|
| 400 | Bad Request | 請求格式錯誤、驗證失敗 |
| 401 | Unauthorized | Token 無效或過期 |
| 403 | Forbidden | 缺少授權許可 |
| 404 | Not Found | 資源不存在 |
| 409 | Conflict | 資源衝突（如重複建立） |
| 500 | Server Error | 內部錯誤 |

### 實用診斷命令

```bash
# 檢查 .NET 專案狀態
dotnet build --verbosity quiet
dotnet test --verbosity minimal

# 檢查 Angular 專案狀態
cd DashboardFrontend
npm run lint
ng build --configuration development

# 檢查 Docker 容器
docker ps
docker logs <container_name>

# 檢查 Qdrant
curl http://localhost:6333/collections

# 檢查 Redis
redis-cli monitor
```
