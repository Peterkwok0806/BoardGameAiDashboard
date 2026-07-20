# AI Agent Skills & Capabilities Context

## My Skill Level (Updated July 2026)

---

## Strong Skills (Production-Ready)

### Backend Development
- .NET 8 Web API + Clean Architecture
- Entity Framework Core (Code-First, Migration, Repository Pattern)
- SQL Server + LINQ + Performance Tuning (UPDLOCK, AsNoTracking, ExecuteUpdateAsync)
- JWT Authentication with Refresh Token Rotation
- RESTful API Design + Swagger/OpenAPI
- Hangfire for background jobs
- Serilog structured logging
- Global exception handling with ProblemDetails (RFC 7807)

### AI & RAG Development
- Semantic Kernel for LLM orchestration
- Azure OpenAI / OpenAI API integration (chat completions & embeddings)
- RAG pipeline design (retrieval-augmented generation)
- Prompt engineering for multilingual responses (Cantonese/Chinese)
- Hybrid search strategies (vector + keyword + metadata filtering)

### Real-time & Caching
- SignalR hubs for real-time features (game updates, live predictions)
- Redis caching patterns (cache-aside, distributed locks, TTL strategies)

### Machine Learning
- Basic ML.NET pipeline development (feature engineering, training, evaluation)
- Win rate prediction models
- Churn prediction basics
- ONNX Runtime for lightweight model inference

### Frontend Development
- Angular 19 with Signals, Standalone Components
- Tailwind CSS (utility-first styling)
- JWT token management (interceptors, guards)
- Chart.js / ng2-charts for data visualization
- SweetAlert2 for confirmation dialogs
- Reactive Forms with validation

### Database & Data
- Soft Delete pattern (IsDeleted, DeletedAt)
- Batch Operations (ExecuteUpdateAsync / ExecuteDeleteAsync)
- Query optimization and indexing strategies
- Database transactions and concurrency control
- JSON columns for metadata storage in SQL Server

### Architecture & Patterns
- Clean Architecture + Repository + Service pattern
- FluentValidation for DTO validation
- ApiResult<T> wrapper pattern
- Pagination patterns
- RAG-first architecture for AI features

### Testing
- xUnit + Moq unit testing
- Playwright E2E testing

### DevOps & Deployment
- Azure App Service + SQL Database deployment
- Git & GitHub workflow
- CI/CD pipeline understanding

---

## Good Knowledge (Use with Care)

### Vector & Embedding Infrastructure
- Qdrant vector database operations (collections, CRUD, search)
- Embedding generation & similarity search
- PDF → semantic chunking pipelines (text extraction, splitting, metadata tagging)
- Rule ingestion workflows (document → chunks → embed → store)

### ML.NET & Predictions
- ML.NET model training (regression, binary classification, clustering)
- Feature engineering for game/match data
- "What-If" simulation logic
- Model evaluation metrics (accuracy, precision, recall, AUC)

### Advanced Patterns
- CQRS (basic understanding)
- MediatR for cross-cutting concerns
- Basic Event Sourcing concepts

### Cloud & Infrastructure
- Docker containerization (basic)
- Azure Blob Storage integration
- Azure Key Vault for secrets
- Azure AI Search (alternative to Qdrant)

### Other
- Prompt Engineering with AI tools
- Basic system design patterns
- Cantonese/Chinese NLP considerations

---

## Learning / Limited Experience

- Advanced System Design (Microservices, CQRS, Event Sourcing)
- Kubernetes orchestration
- Advanced ML Engineering (custom ONNX training, deep learning)
- Mobile Development (MAUI / Flutter)
- GraphQL API design
- Advanced SignalR patterns (backplane, scaling)
- Distributed caching strategies at scale

---

## Development Preferences

### Code Style
- Prefer Clean Architecture + Repository + Service pattern
- Always use async/await (never .Result or .Wait())
- Use ApiResult<T> or ApiResult for consistent API responses
- Add XML comments for public methods
- Use FluentValidation for DTO validation
- Follow existing code style and naming conventions

### AI & RAG
- RAG-first approach for AI features (retrieve context before generating)
- Hybrid search (vector similarity + keyword matching + metadata filtering)
- Always filter vector searches by game/topic metadata
- Support Cantonese/Chinese responses with proper language detection
- Chunk documents intelligently (not whole PDFs into LLM)
- Use Semantic Kernel for LLM orchestration over raw API calls
- Log all RAG queries and responses for debugging & improvement

### ML & Predictions
- Keep models lightweight; prefer ONNX for inference
- Feature engineer from match history data
- Always validate model performance before deployment
- Cache prediction results where appropriate

### Performance
- Prefer bulk operations (ExecuteUpdateAsync / ExecuteDeleteAsync)
- Use AsNoTracking() for read-only queries
- Use UPDLOCK for critical race condition prevention
- Cache expensive computations with Redis (with proper TTL)

### Quality
- Production-ready, clean, maintainable code
- Comprehensive error handling
- Structured logging for debugging
- User feedback loops for RAG improvement

### Process
- Iterative development (small steps + review)
- I review and improve AI-generated code
- Prefer incremental improvements over large refactors

---

## AI Usage Style

- I use AI to accelerate development (boilerplate, refactoring, testing)
- I always review and improve AI-generated code
- I want production-ready, clean, maintainable code
- I prefer iterative development (small steps + review)

---

## Instruction for AI Agent

When generating code, always consider my skill level above:

### DO:
- Use patterns I know well (Clean Architecture, Repository, Service)
- Use Semantic Kernel for LLM orchestration
- Suggest incremental improvements
- Provide code with XML comments
- Include validation (FluentValidation)
- Show bulk operation alternatives for performance
- Add error handling best practices
- Use metadata filtering in all vector searches
- Support multilingual (Cantonese/Chinese) in AI responses

### DON'T:
- Use overly advanced patterns I haven't implemented yet (unless I ask)
- Generate complex microservices architecture
- Use patterns requiring Docker/Kubernetes knowledge
- Suggest GraphQL when REST would work
- Use blocking calls (.Result, .Wait())
- Dump entire documents into LLM context without chunking
- Use raw HTTP calls for OpenAI when Semantic Kernel is available

### Consider Adding:
- Performance tips for EF Core
- Pagination for list endpoints
- Global exception handling
- Health check endpoints (including Vector DB connectivity)
- Structured logging setup
- E2E test examples with Playwright
- RAG evaluation metrics (relevance, faithfulness)
- Vector search performance benchmarks

---

## Quick Reference

### Preferred Patterns
```
✅ Good                          ❌ Avoid
---                              ----
async/await                      .Result, .Wait()
ApiResult<T>                     raw objects
FluentValidation                 manual validation
ExecuteUpdateAsync               loop + Update
AsNoTracking()                   default tracking
UPDLOCK hint                     no locking
Serilog                          Console.WriteLine
ProblemDetails                   generic errors
Semantic Kernel                  raw OpenAI HTTP calls
Hybrid search                    pure vector-only search
Metadata filtering               no context filtering
Chunk + embed + store            dump entire PDF into LLM
Redis caching                    recompute every request
```

### File Locations
- Solution: `BoardGameAiDashboard/BoardGameAiDashboard.sln`
- API Layer: `BoardGameAiDashboard/BoardGameAiDashboard.Api/`
- Domain Layer: `BoardGameAiDashboard/BoardGameAiDashboard.Domain/`
- Application Layer: `BoardGameAiDashboard/BoardGameAiDashboard.Application/`
- Infrastructure Layer: `BoardGameAiDashboard/BoardGameAiDashboard.Infrastructure/`
- Tests (future): `BoardGameAiDashboard.Tests/`

### Key Technologies
- .NET 8, EF Core, SQL Server
- Semantic Kernel, Azure OpenAI, Qdrant
- ML.NET, ONNX Runtime
- SignalR, Redis, Hangfire, Serilog
- xUnit, Moq, Playwright
- Azure App Service, Azure SQL
