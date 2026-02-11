using Fixon.Domain.Companies;
using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fixon.IntegrationTests.Infrastructure;

public static class TestDbSeeder
{
    private static readonly SemaphoreSlim MigrateLock = new(1, 1);
    private static bool _migrated;

    private static readonly SemaphoreSlim RoleLock = new(1, 1);
    private static bool _adminRoleEnsured;

    public static async Task EnsureMigratedAsync(IServiceProvider rootServices, CancellationToken ct = default)
    {
        if (_migrated) return;

        await MigrateLock.WaitAsync(ct);
        try
        {
            if (_migrated) return;

            using var scope = rootServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FixonDbContext>();
            await db.Database.MigrateAsync(ct);

            _migrated = true;
        }
        finally
        {
            MigrateLock.Release();
        }
    }

    public static async Task<SeededAdmin> SeedAdminAsync(
        IServiceProvider rootServices,
        string password,
        CancellationToken ct = default)
    {
        await EnsureMigratedAsync(rootServices, ct);

        using var scope = rootServices.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is not configured for tests.");
        }

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Use system context so query filters won't block reads/writes.
        await using var db = new FixonDbContext(options, new SystemTenantProvider());

        var now = DateTimeOffset.UtcNow;
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var email = $"admin+{companyId:n}@integration.test";
        var company = new Company(companyId, $"Integration Test Company {companyId:n}", now, isActive: true);

        var adminRole = await EnsureAdminRoleAsync(db, ct);

        var passwordHasher = new PasswordHasher();
        var user = new User(
            id: userId,
            companyId: companyId,
            email: email,
            name: "Integration Test Admin",
            passwordHash: passwordHasher.HashPassword(password),
            createdAt: now,
            isActive: true);

        db.Companies.Add(company);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole(userId: userId, roleId: adminRole.Id));

        await db.SaveChangesAsync(ct);

        return new SeededAdmin(companyId, userId, email, password);
    }

    private static async Task<Role> EnsureAdminRoleAsync(FixonDbContext db, CancellationToken ct)
    {
        if (_adminRoleEnsured)
        {
            return await db.Roles.SingleAsync(r => r.Name == Roles.Admin, ct);
        }

        await RoleLock.WaitAsync(ct);
        try
        {
            // Double-check inside lock
            var existing = await db.Roles.SingleOrDefaultAsync(r => r.Name == Roles.Admin, ct);
            if (existing is not null)
            {
                _adminRoleEnsured = true;
                return existing;
            }

            db.Roles.Add(new Role(Guid.NewGuid(), Roles.Admin));

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                // Another concurrent test created the role.
                db.ChangeTracker.Clear();
            }

            var role = await db.Roles.SingleAsync(r => r.Name == Roles.Admin, ct);
            _adminRoleEnsured = true;
            return role;
        }
        finally
        {
            RoleLock.Release();
        }
    }
}

public sealed record SeededAdmin(
    Guid CompanyId,
    Guid UserId,
    string Email,
    string Password);

