using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

internal static partial class AccountRules
{
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    /// <summary>Unguessable token for a link (URL-safe).</summary>
    public static string NewLinkToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // No 0/O, 1/I/L: codes are read aloud and typed on a phone.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NewPairingCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return $"{chars[..4]}-{chars[4..]}";
    }

    /// <summary>"k7qx m2pd", "K7QX-M2PD" and "k7qxm2pd" are the same code.</summary>
    public static string NormalizeCode(string code) => NonCode().Replace(code.ToUpperInvariant(), string.Empty);

    public static string HashCode(string normalized) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

    public static string Slugify(string name)
    {
        var slug = NonSlug().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "company" : slug[..Math.Min(slug.Length, 40)];
    }

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NonCode();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}

internal static class AccountMapping
{
    public static async Task<AccountDto> ToDtoAsync(UserAccount user, IAccountRepository accounts, CancellationToken cancellationToken) =>
        new(
            user.Id,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            user.OrganizationId,
            await accounts.OrganizationNameAsync(user.OrganizationId, cancellationToken) ?? string.Empty,
            await accounts.DefaultFleetIdAsync(user.OrganizationId, cancellationToken),
            user.DriverId);
}

public class SignUpCommandHandler : IRequestHandler<SignUpCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _clock;
    private readonly ILogger<SignUpCommandHandler> _logger;

    public SignUpCommandHandler(IAccountRepository accounts, IPasswordHasher hasher, TimeProvider clock, ILogger<SignUpCommandHandler> logger)
    {
        _accounts = accounts;
        _hasher = hasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AccountDto> Handle(SignUpCommand request, CancellationToken cancellationToken)
    {
        var email = AccountRules.NormalizeEmail(request.Email);
        if (await _accounts.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new BusinessRuleException("An account with this email already exists. Sign in instead.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var slug = AccountRules.Slugify(request.CompanyName);
        while (await _accounts.SlugExistsAsync(slug, cancellationToken))
        {
            slug = $"{AccountRules.Slugify(request.CompanyName)}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        }

        var organization = new Organization { Id = Guid.NewGuid(), Name = request.CompanyName.Trim(), Slug = slug, CreatedAtUtc = now };
        var workspace = new Workspace { Id = Guid.NewGuid(), OrganizationId = organization.Id, Name = "Main", CreatedAtUtc = now };
        var fleet = new Fleet
        {
            Id = Guid.NewGuid(), OrganizationId = organization.Id, WorkspaceId = workspace.Id, Name = "Main fleet", CreatedAtUtc = now,
        };
        workspace.DefaultFleetId = fleet.Id;

        var owner = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Email = email,
            DisplayName = request.Name.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Owner,
            CreatedAtUtc = now,
            LastSignInAtUtc = now,
        };

        await _accounts.AddOrganizationAsync(organization, workspace, fleet, cancellationToken);
        await _accounts.AddAsync(owner, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("New company account {OrganizationId} ({Slug})", organization.Id, slug);

        return new AccountDto(owner.Id, owner.DisplayName, owner.Email, owner.Role.ToString(), organization.Id, organization.Name, fleet.Id, null);
    }
}

public class SignInCommandHandler : IRequestHandler<SignInCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _clock;

    public SignInCommandHandler(IAccountRepository accounts, IPasswordHasher hasher, TimeProvider clock)
    {
        _accounts = accounts;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<AccountDto> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var user = await _accounts.GetByEmailAsync(AccountRules.NormalizeEmail(request.Email), cancellationToken);
        if (user?.PasswordHash is null || !user.IsActive)
        {
            _hasher.Verify(request.Password, _dummy ??= _hasher.Hash("not-a-real-password"), out _);
            throw new AuthenticationFailedException();
        }

        if (user.LockedUntilUtc > now)
        {
            throw new AuthenticationFailedException("Too many attempts. Wait 15 minutes and try again.");
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash, out var needsRehash))
        {
            user.FailedSignIns++;
            if (user.FailedSignIns >= AccountRules.MaxFailedSignIns)
            {
                user.LockedUntilUtc = now + AccountRules.Lockout;
                user.FailedSignIns = 0;
            }

            await _accounts.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        if (needsRehash)
        {
            user.PasswordHash = _hasher.Hash(request.Password);
        }

        user.FailedSignIns = 0;
        user.LockedUntilUtc = null;
        user.LastSignInAtUtc = now;
        await _accounts.SaveChangesAsync(cancellationToken);
        return await AccountMapping.ToDtoAsync(user, _accounts, cancellationToken);
    }

    // Verified when the email is unknown, so a wrong email takes as long as a wrong password.
    private static string? _dummy;
}

public class CreateDriverPairingCodeCommandHandler : IRequestHandler<CreateDriverPairingCodeCommand, DriverPairingCodeDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IDriverRepository _drivers;
    private readonly ITenantScope _scope;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public CreateDriverPairingCodeCommandHandler(
        IAccountRepository accounts, IDriverRepository drivers, ITenantScope scope, ITenantContext tenant, IAuditService audit, TimeProvider clock)
    {
        _accounts = accounts;
        _drivers = drivers;
        _scope = scope;
        _tenant = tenant;
        _audit = audit;
        _clock = clock;
    }

    public async Task<DriverPairingCodeDto> Handle(CreateDriverPairingCodeCommand request, CancellationToken cancellationToken)
    {
        await _scope.EnsureDriverAsync(request.DriverId, cancellationToken);
        var driver = await _drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Driver {request.DriverId} not found.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var code = AccountRules.NewPairingCode();
        var pairing = new DriverPairingCode
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            DriverId = driver.Id,
            CodeHash = AccountRules.HashCode(AccountRules.NormalizeCode(code)),
            ExpiresAtUtc = now + AccountRules.PairingCodeLifetime,
            CreatedByUserId = _tenant.UserId,
            CreatedAtUtc = now,
        };
        await _accounts.AddPairingCodeAsync(pairing, cancellationToken);
        await _audit.LogAsync("CreatePairingCode", "Driver", driver.Id.ToString(), _tenant.UserId, null, cancellationToken);
        return new DriverPairingCodeDto(code, pairing.ExpiresAtUtc, driver.FullName);
    }
}

public class PairDriverCommandHandler : IRequestHandler<PairDriverCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IDriverRepository _drivers;
    private readonly TimeProvider _clock;

    public PairDriverCommandHandler(IAccountRepository accounts, IDriverRepository drivers, TimeProvider clock)
    {
        _accounts = accounts;
        _drivers = drivers;
        _clock = clock;
    }

    public async Task<AccountDto> Handle(PairDriverCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var pairing = await _accounts.GetPairingCodeAsync(AccountRules.HashCode(AccountRules.NormalizeCode(request.Code)), cancellationToken);
        if (pairing is null || pairing.UsedAtUtc is not null || pairing.ExpiresAtUtc <= now)
        {
            throw new AuthenticationFailedException("That code is not valid or has expired. Ask the office for a new one.");
        }

        var driver = await _drivers.GetByIdAsync(pairing.DriverId, cancellationToken)
            ?? throw new AuthenticationFailedException("That code is not valid or has expired. Ask the office for a new one.");

        pairing.UsedAtUtc = now;
        var account = await _accounts.GetDriverAccountAsync(pairing.OrganizationId, driver.Id, cancellationToken);
        if (account is null)
        {
            account = new UserAccount
            {
                Id = Guid.NewGuid(),
                OrganizationId = pairing.OrganizationId,
                DisplayName = driver.FullName,
                Role = UserRole.Driver,
                DriverId = driver.Id,
                CreatedAtUtc = now,
            };
            await _accounts.AddAsync(account, cancellationToken);
        }

        account.IsActive = true;
        account.DisplayName = driver.FullName;
        account.LastSignInAtUtc = now;
        await _accounts.SaveChangesAsync(cancellationToken);
        return await AccountMapping.ToDtoAsync(account, _accounts, cancellationToken);
    }
}

public class GetAccountQueryHandler : IRequestHandler<GetAccountQuery, AccountDto?>
{
    private readonly IAccountRepository _accounts;

    public GetAccountQueryHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<AccountDto?> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        var user = await _accounts.GetByIdAsync(request.UserId, cancellationToken);
        return user is { IsActive: true } ? await AccountMapping.ToDtoAsync(user, _accounts, cancellationToken) : null;
    }
}
