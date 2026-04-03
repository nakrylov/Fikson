using Fixon.Api.Security;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Endpoints;

public static class FactTypesEndpoints
{
    [Authorize(Policy = Permissions.ContractsRead)]
    [RequireTenant]
    public static async Task<IResult> GetFactTypes(
        FixonDbContext db,
        CancellationToken ct)
    {
        var items = await db.FactTypeDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .Select(x => new
            {
                eventType = x.EventType,
                displayName = x.DisplayName,
                valueType = x.ValueType,
                defaultConditionType = x.DefaultConditionType,
                unit = x.Unit
            })
            .ToListAsync(ct);

        return Results.Ok(items);
    }
}
