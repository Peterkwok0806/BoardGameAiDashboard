# ─────────────────────────────────────────────────────────────────────────────
# Build Stage
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy solution and project files first (for better Docker layer caching)
COPY BoardGameAiDashboard.sln BoardGameAiDashboard.sln
COPY BoardGameAiDashboard.Api/BoardGameAiDashboard.Api.csproj BoardGameAiDashboard.Api/BoardGameAiDashboard.Api.csproj
COPY BoardGameAiDashboard.Application/BoardGameAiDashboard.Application.csproj BoardGameAiDashboard.Application/BoardGameAiDashboard.Application.csproj
COPY BoardGameAiDashboard.Infrastructure/BoardGameAiDashboard.Infrastructure.csproj BoardGameAiDashboard.Infrastructure/BoardGameAiDashboard.Infrastructure.csproj
COPY BoardGameAiDashboard.Domain/BoardGameAiDashboard.Domain.csproj BoardGameAiDashboard.Domain/BoardGameAiDashboard.Domain.csproj

# 只 restore API 專案（不要用整個 solution）
RUN dotnet restore BoardGameAiDashboard.Api/BoardGameAiDashboard.Api.csproj

# Copy source code
COPY . .

# Build and publish
WORKDIR /src/BoardGameAiDashboard.Api
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish --no-build

# ─────────────────────────────────────────────────────────────────────────────
# Runtime Stage
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy published output
COPY --from=build /app/publish .

# Expose port (internal only, accessed via Nginx)
EXPOSE 8080

ENTRYPOINT ["dotnet", "BoardGameAiDashboard.Api.dll"]
