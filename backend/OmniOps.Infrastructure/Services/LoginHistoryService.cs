using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class LoginHistoryService : ILoginHistoryService
{
    private readonly AppDbContext _context;

    public LoginHistoryService(AppDbContext context) => _context = context;

    public async Task RecordAsync(string userId, string? ipAddress, bool success, CancellationToken cancellationToken = default)
    {
        await _context.LoginHistories.AddAsync(new LoginHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IpAddress = ipAddress,
            Success = success,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
