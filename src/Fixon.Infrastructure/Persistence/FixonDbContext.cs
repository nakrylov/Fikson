using System.Reflection;
using Fixon.Domain.Abstractions;
using Fixon.Domain.Audit;
using Fixon.Domain.Claims;
using Fixon.Domain.Companies;
using Fixon.Domain.Contracts;
using Fixon.Domain.Facts;
using Fixon.Domain.Sla;
using Fixon.Domain.Users;
using Fixon.Infrastructure.Tenancy;
using Fixon.Infrastructure.BackgroundJobs;
using Fixon.Domain.Penalties;
using Fixon.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Infrastructure.Persistence;

public sealed class FixonDbContext : DbContext
{
    private readonly Guid? _companyId;
    private readonly bool _bypassTenantFilter;
    private readonly bool _bypassIsActiveFilter;

    public FixonDbContext(DbContextOptions<FixonDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _companyId = tenantProvider.TenantId;
        _bypassTenantFilter = tenantProvider.IsSystemContext;
        _bypassIsActiveFilter = tenantProvider.IsSystemContext;
    }

    // DbSets
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<TenantInvite> TenantInvites => Set<TenantInvite>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();
    public DbSet<Counterparty> Counterparties => Set<Counterparty>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractVersion> ContractVersions => Set<ContractVersion>();
    public DbSet<SlaRule> SlaRules => Set<SlaRule>();
    public DbSet<SlaRuleVersion> SlaRuleVersions => Set<SlaRuleVersion>();
    public DbSet<Fact> Facts => Set<Fact>();
    public DbSet<FactImport> FactImports => Set<FactImport>();
    public DbSet<SlaEvaluation> SlaEvaluations => Set<SlaEvaluation>();
    public DbSet<SlaViolation> SlaViolations => Set<SlaViolation>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimDecision> ClaimDecisions => Set<ClaimDecision>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<Penalty> Penalties => Set<Penalty>();
    public DbSet<PenaltyAdjustment> PenaltyAdjustments => Set<PenaltyAdjustment>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyGlobalDeleteBehaviorRestrict(modelBuilder);
        ApplyGlobalQueryFilters(modelBuilder);
    }

    private void ApplyGlobalDeleteBehaviorRestrict(ModelBuilder modelBuilder)
    {
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Multi-tenancy: centralized CompanyId filter (см. docs/04-auth-multitenancy.md)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (clrType == typeof(Company))
            {
                ApplyIsActiveFilterForCompany(modelBuilder);
                continue;
            }

            // SaaS (Model B): User is a global identity (no tenant filter), but still activatable.
            if (clrType == typeof(User))
            {
                modelBuilder.Entity<User>()
                    .HasQueryFilter(u => _bypassIsActiveFilter || u.IsActive);
                continue;
            }

            if (typeof(ActivatableTenantEntity).IsAssignableFrom(clrType))
            {
                InvokeGeneric(nameof(ApplyTenantAndIsActiveFilter), clrType, modelBuilder);
                continue;
            }

            if (typeof(TenantEntity).IsAssignableFrom(clrType))
            {
                InvokeGeneric(nameof(ApplyTenantFilter), clrType, modelBuilder);
                continue;
            }

            // UserRole не содержит CompanyId (см. docs/02-er-diagram.md), но доступ должен быть tenant-scoped.
            // Фильтруем через навигацию на User.
            if (clrType == typeof(UserRole))
            {
                ApplyUserRoleTenantFilter(modelBuilder);
            }

            // SaaS (step 1): membership uses TenantId (not CompanyId), so we apply explicit filter.
            if (clrType == typeof(UserTenantMembership))
            {
                ApplyUserTenantMembershipFilter(modelBuilder);
            }
        }
    }

    private void ApplyIsActiveFilterForCompany(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>()
            .HasQueryFilter(c => _bypassIsActiveFilter || c.IsActive);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : TenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => _bypassTenantFilter || (_companyId.HasValue && e.CompanyId == _companyId.GetValueOrDefault()));
    }

    private void ApplyTenantAndIsActiveFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : ActivatableTenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e =>
                (_bypassTenantFilter || (_companyId.HasValue && e.CompanyId == _companyId.GetValueOrDefault()))
                && (_bypassIsActiveFilter || e.IsActive));
    }

    private void ApplyUserRoleTenantFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>()
            .HasQueryFilter(ur =>
                _bypassTenantFilter
                || (_companyId.HasValue && ur.User != null && ur.User.CompanyId.HasValue && ur.User.CompanyId.Value == _companyId.GetValueOrDefault()));
    }

    private void ApplyUserTenantMembershipFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserTenantMembership>()
            .HasQueryFilter(m => _bypassTenantFilter || (_companyId.HasValue && m.TenantId == _companyId.GetValueOrDefault()));
    }

    private void InvokeGeneric(string methodName, Type clrType, ModelBuilder modelBuilder)
    {
        var method = GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (method is null)
        {
            throw new InvalidOperationException($"Method '{methodName}' not found.");
        }

        var generic = method.MakeGenericMethod(clrType);
        generic.Invoke(this, new object[] { modelBuilder });
    }
}


