using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRedisPermissionService _permissionService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenBlacklistService _blacklistService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        IJwtService jwtService,
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IRedisPermissionService permissionService,
        IIdempotencyService idempotencyService,
        IPasswordHasher passwordHasher,
        ITokenBlacklistService blacklistService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _db = db;
        _jwtService = jwtService;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _permissionService = permissionService;
        _idempotencyService = idempotencyService;
        _passwordHasher = passwordHasher;
        _blacklistService = blacklistService;
        _configuration = configuration;
        _logger = logger;
    }

    // ───────────────────────────────── LOGIN ─────────────────────────────────

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            return Unauthorized(new { message = "Invalid credentials" });

        if (user.IsLocked && user.LockedUntil > DateTimeOffset.UtcNow)
            return StatusCode(423, new { message = "Account locked", lockedUntil = user.LockedUntil });

        if (user.IsLocked && user.LockedUntil <= DateTimeOffset.UtcNow)
        {
            user.IsLocked = false;
            user.LoginFailCount = 0;
        }

        if (!await _passwordHasher.VerifyAsync(request.Password, user.PasswordHash))
        {
            user.LoginFailCount++;
            var maxAttempts = _configuration.GetValue("Security:MaxLoginAttempts", 5);
            if (user.LoginFailCount >= maxAttempts)
            {
                var lockDuration = TimeSpan.FromMinutes(15 * Math.Pow(2, user.LockCount));
                user.IsLocked = true;
                user.LockedUntil = DateTimeOffset.UtcNow.Add(lockDuration);
                user.LockCount++;
                _logger.LogWarning("Account locked for {Email} after {Attempts} failed attempts",
                    user.Email, user.LoginFailCount);
            }
            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (user.MfaEnabled)
        {
            var mfaToken = _accessTokenService.CreateMfaToken(user.Id);
            return Ok(new MfaChallengeResponse(true, mfaToken));
        }

        return Ok(await IssueTokenPairAsync(user));
    }

    // ───────────────────────────────── REFRESH ─────────────────────────────────

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            var (userId, newRawToken) = await _refreshTokenService.RefreshAsync(request.RefreshToken);
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.Id == userId);

            if (user.IsLocked)
                return Unauthorized(new { message = "Account is locked" });

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            await _permissionService.SetPermissionsAsync(userId, roles);

            var accessToken = _accessTokenService.CreateToken(
                user.Id, user.Email,
                string.Join(",", roles), string.Join(" ", roles));

            return Ok(new { accessToken, refreshToken = newRawToken });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Refresh token failure: {Reason}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    // ───────────────────────────────── LOGOUT ─────────────────────────────────

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        var jti = User.FindFirst("jti")?.Value;
        var expClaim = User.FindFirst("exp")?.Value;

        // Revoke refresh token
        await _refreshTokenService.RevokeAsync(request.RefreshToken);

        // Remove permissions from Redis
        if (userId != null)
        {
            await _permissionService.RemovePermissionsAsync(Guid.Parse(userId));
        }

        // Blacklist access token JTI
        if (jti != null && expClaim != null && long.TryParse(expClaim, out var expUnixTime))
        {
            var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expUnixTime).UtcDateTime;
            var remainingTtl = expDateTime - DateTime.UtcNow;
            if (remainingTtl > TimeSpan.Zero)
            {
                await _blacklistService.BlacklistAsync(jti, remainingTtl);
            }
        }

        return Ok(new { message = "Logged out" });
    }

    // ───────────────────────────────── REGISTER ─────────────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var cached = await _idempotencyService.GetCachedResponseAsync(idempotencyKey);
            if (cached != null) return Ok(cached);
        }

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Email already registered" });

        var passwordHash = await _passwordHasher.HashAsync(request.Password);
        var user = new User { Email = request.Email, PasswordHash = passwordHash };
        _db.Users.Add(user);

        var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "user");
        if (defaultRole != null)
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });

        await _db.SaveChangesAsync();
        var result = new { message = "User registered", userId = user.Id };

        if (!string.IsNullOrEmpty(idempotencyKey))
            await _idempotencyService.StoreAsync(idempotencyKey, result);

        return Created($"/users/{user.Id}", result);
    }

    // ───────────────────────────────── MFA (Phase 2) ─────────────────────────────────

    [HttpPost("mfa")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaRequest request)
    {
        var principal = _jwtService.ValidateToken(request.MfaToken);
        var userId = principal?.FindFirst("sub")?.Value;
        var purpose = principal?.FindFirst("purpose")?.Value;

        if (userId == null || purpose != "mfa")
            return Unauthorized(new { message = "Invalid MFA token" });

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

        if (user == null || string.IsNullOrEmpty(user.TotpSecret))
            return Unauthorized(new { message = "MFA not configured" });

        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(user.TotpSecret));
        if (!totp.VerifyTotp(request.Code, out _, new OtpNet.VerificationWindow(1, 1)))
            return Unauthorized(new { message = "Invalid TOTP code" });

        return Ok(await IssueTokenPairAsync(user));
    }

    // ───────────────────────────────── INTROSPECT ─────────────────────────────────

    [Authorize]
    [HttpPost("introspect")]
    public IActionResult Introspect([FromBody] IntrospectRequest request)
    {
        var principal = _jwtService.ValidateToken(request.Token);
        if (principal == null)
            return Ok(new IntrospectResponse(false));

        return Ok(new IntrospectResponse(
            Active: true,
            Sub: principal.FindFirst("sub")?.Value,
            Scope: principal.FindFirst("scope")?.Value,
            Exp: principal.FindFirst("exp")?.Value));
    }

    // ───────────────────────────────── REVOKE ─────────────────────────────────

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (userId != null)
        {
            await _refreshTokenService.RevokeAllForUserAsync(Guid.Parse(userId));
            await _permissionService.RemovePermissionsAsync(Guid.Parse(userId));
        }
        return Ok(new { message = "All tokens revoked" });
    }

    // ───────────────────────────────── HELPERS ─────────────────────────────────

    private async Task<TokenPairResponse> IssueTokenPairAsync(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        user.LoginFailCount = 0;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await _permissionService.SetPermissionsAsync(user.Id, roles);

        var accessToken = _accessTokenService.CreateToken(
            user.Id, user.Email,
            string.Join(",", roles), string.Join(" ", roles));

        var refreshToken = await _refreshTokenService.CreateAsync(user.Id);

        return new TokenPairResponse(accessToken, refreshToken,
            new UserInfoResponse(user.Id, user.Email, roles));
    }
}
