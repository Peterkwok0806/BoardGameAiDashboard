---
name: jwt-auth
description: 實作認證、登入、註冊、token 刷新，或任何本專案的安全相關功能
---

## 架構概覽

本專案使用 JWT（JSON Web Token）認證搭配 refresh token 輪換：

```
┌────────┐     ┌────────────┐     ┌────────────┐
│ Client │────▶│ /api/auth/ │────▶│ 驗證       │
│        │     │ login      │     │ 憑證       │
└────────┘     └────────────┘     └─────┬──────┘
                                        │
                   ┌────────────────────┘
                   ▼
            ┌────────────┐
            │ 回傳：     │
            │ - Access   │  (15 分鐘 TTL)
            │   Token    │
            │ - Refresh  │  (7 天 TTL)
            │   Token    │
            └────────────┘

後續請求：
  Header: Authorization: Bearer <access_token>
  
Token 刷新：
  POST /api/auth/refresh
  Body: { refreshToken: "..." }
  → 新的 access + refresh token 配對
```

### 關鍵檔案

| 元件 | 檔案 | 用途 |
|------|------|------|
| 使用者實體 | `Domain/Entities/User.cs` | 使用者資料模型 |
| RefreshToken | `Domain/Entities/RefreshToken.cs` | Refresh token 儲存 |
| JWT Service | `Infrastructure/Services/Auth/JwtTokenService.cs` | Token 生成/驗證 |
| 密碼雜湊器 | `Infrastructure/Services/Auth/BCryptPasswordHasher.cs` | 密碼雜湊 |
| 認證 Commands | `Application/Features/Auth/` | Login、Register、Refresh handlers |
| 認證 Controller | `Api/Controllers/AuthController.cs` | 認證端點 |
| DI 註冊 | `Infrastructure/DependencyInjection.cs` | JWT 設定 |

### 設定

```json
// appsettings.json
{
  "Jwt": {
    "Secret": "YOUR_SUPER_SECRET_KEY_MIN_32_CHARS_LONG!!",
    "Issuer": "BoardGameAiDashboard",
    "Audience": "BoardGameAiDashboard",
    "AccessTokenTTLMinutes": 15,
    "RefreshTokenTTLDays": 7
  }
}
```

## 必要模式

### 1. JWT Token 生成

```csharp
// Infrastructure/Services/Auth/JwtTokenService.cs
public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task<bool> ValidateTokenAsync(string token);
    Task<Guid?> GetUserIdFromTokenAsync(string token);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;
    
    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        _signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.Secret));
    }
    
    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("display_name", user.DisplayName)
        };
        
        var credentials = new SigningCredentials(
            _settings: _signingKey, 
            SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenTTLMinutes),
            signingCredentials: credentials);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
```

### 2. Refresh Token 輪換（單次使用）

```csharp
// Application/Features/Auth/RefreshTokenCommandHandler.cs
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, ApiResult<TokenPairResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    
    public async Task<ApiResult<TokenPairResponse>> Handle(
        RefreshTokenCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. 找到 refresh token
        var refreshToken = await _unitOfWork.RefreshTokens
            .GetByTokenAsync(request.RefreshToken);
        
        if (refreshToken == null || refreshToken.IsExpired)
        {
            throw new UnauthorizedException("Invalid refresh token");
        }
        
        // 2. 檢查 token 是否被撤銷（單次使用檢查）
        if (refreshToken.IsRevoked)
        {
            // 安全性：撤銷此使用者的所有 tokens（可能被盗用）
            await _unitOfWork.RefreshTokens
                .RevokeAllForUserAsync(refreshToken.UserId);
            throw new UnauthorizedException("Token has been revoked");
        }
        
        // 3. 取得使用者
        var user = await _unitOfWork.Users.GetByIdAsync(refreshToken.UserId);
        if (user == null)
        {
            throw new UnauthorizedException("User not found");
        }
        
        // 4. 撤銷舊 token（單次使用輪換）
        await _unitOfWork.RefreshTokens.RevokeAsync(refreshToken);
        
        // 5. 生成新的 token 配對
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        
        // 6. 儲存新的 refresh token
        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenTTLDays),
            CreatedByIp = request.IpAddress
        });
        
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResult<TokenPairResponse>.Success(new TokenPairResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = _jwtSettings.AccessTokenTTLMinutes * 60
        });
    }
}
```

### 3. RefreshToken 實體

```csharp
// Domain/Entities/RefreshToken.cs
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string? CreatedByIp { get; private set; }
    
    // 導航屬性
    public User User { get; private set; } = null!;
    
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsExpired && !IsRevoked;
    
    private RefreshToken() { }
    
    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt, string createdByIp)
    {
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp
        };
        refreshToken.SetCreated();
        return refreshToken;
    }
    
    public void Revoke(string revokedByIp, string? replacedByToken = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByToken = replacedByToken;
        base.Delete(); // 軟刪除被撤銷的 token
    }
}
```

### 4. 登入 Command

```csharp
// Application/Features/Auth/LoginUserCommand.cs
public record LoginUserCommand(string Email, string Password) 
    : IRequest<ApiResult<TokenPairResponse>>;

// Application/Features/Auth/LoginUserCommandHandler.cs
public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, ApiResult<TokenPairResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    
    public async Task<ApiResult<TokenPairResponse>> Handle(
        LoginUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password");
        }
        
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        
        await _unitOfWork.RefreshTokens.AddAsync(RefreshToken.Create(
            user.Id,
            refreshToken,
            DateTime.UtcNow.AddDays(7),
            ipAddress));
        
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResult<TokenPairResponse>.Success(new TokenPairResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 15 * 60
        });
    }
}
```

### 5. 使用者實體

```csharp
// Domain/Entities/User.cs
public class User : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    
    // 導航屬性
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    
    private User() { }
    
    public static User Create(string email, string displayName, string passwordHash)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwordHash
        };
        user.SetCreated();
        return user;
    }
}
```

### 6. 密碼雜湊

```csharp
// Infrastructure/Services/Auth/BCryptPasswordHasher.cs
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }
    
    public bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
```

### 7. JWT DI 註冊

```csharp
// Infrastructure/DependencyInjection.cs
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

services.AddAuthorization();

services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
services.AddScoped<IJwtTokenService, JwtTokenService>();
```

### 8. 認證 Controller

```csharp
// Api/Controllers/AuthController.cs
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
    
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
    
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
```

### 9. JWT Middleware 設定

```csharp
// Api/Program.cs
app.UseAuthentication();
app.UseAuthorization();
```

### 10. 取得目前使用者

```csharp
// Application/Features/Auth/GetCurrentUserQuery.cs
public record GetCurrentUserQuery : IRequest<ApiResult<UserDto>>;

// Application/Features/Auth/GetCurrentUserQueryHandler.cs
public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, ApiResult<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<ApiResult<UserDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(); // 從 claims 擷取
        
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), userId);
        }
        
        return ApiResult<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        });
    }
}
```

## 測試認證

```csharp
[Fact]
public async Task RefreshToken_IsSingleUse()
{
    // Arrange
    var user = User.Create("test@example.com", "Test", _hasher.Hash("password"));
    var refreshToken = RefreshToken.Create(user.Id, "token123", DateTime.UtcNow.AddDays(7), "127.0.0.1");
    
    // Act — 第一次使用應該成功
    var result1 = await _handler.Handle(new RefreshTokenCommand("token123"), ct);
    
    // Act — 第二次使用應該失敗
    var act = () => _handler.Handle(new RefreshTokenCommand("token123"), ct);
    
    // Assert
    Assert.True(result1.IsSuccess);
    await Assert.ThrowsAsync<UnauthorizedException>(act);
}
```

## 安全性檢查清單

- [ ] 密碼使用 BCrypt 雜湊（cost factor 12）
- [ ] JWT secret 至少 32 個字元
- [ ] Access token TTL = 15 分鐘
- [ ] Refresh token TTL = 7 天
- [ ] Refresh tokens 為單次使用（輪換）
- [ ] JWT 驗證時 ClockSkew = TimeSpan.Zero
- [ ] RefreshTokens 使用 `IsDeleted` 查詢過濾器
- [ ] Token 建立/撤銷時記錄 IP 位址
- [ ] 所有認證失敗使用 UnauthorizedException
