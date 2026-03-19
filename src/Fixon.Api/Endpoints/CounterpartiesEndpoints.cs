using Fixon.Domain.Contracts;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Endpoints;

public static class CounterpartiesEndpoints
{
    [Authorize(Policy = Permissions.ContractsRead)]
    public static async Task<IResult> GetCounterparties(
        FixonDbContext db,
        CancellationToken ct)
    {
        var items = await db.Counterparties
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name
            })
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> CreateCounterparty(
        [FromBody] CreateCounterpartyRequest request,
        UserContext userContext,
        FixonDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name is required." });
        }

        var counterparty = new Counterparty(
            id: Guid.NewGuid(),
            companyId: userContext.TenantId,
            name: request.Name.Trim(),
            externalCode: null,
            isActive: true);

        db.Counterparties.Add(counterparty);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id = counterparty.Id,
            name = counterparty.Name
        });
    }
}

public sealed class CreateCounterpartyRequest
{
    public string Name { get; set; } = null!;
}
