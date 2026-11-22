using Microsoft.EntityFrameworkCore;

namespace OmniOps.Application.Common.Interfaces;

public static class QueryExtensions
{
    /// <summary>Looks across all companies. Only for anonymous flows with their own proof (a token, a password) and background work.</summary>
    public static IQueryable<T> AcrossCompanies<T>(this IQueryable<T> query) where T : class =>
        query.IgnoreQueryFilters([IAppDbContext.TenantFilter]);
}
