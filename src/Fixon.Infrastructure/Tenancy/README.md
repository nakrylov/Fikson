# Multi-Tenancy Infrastructure

## Архитектура

Multi-tenancy реализована согласно `docs/04-auth-multitenancy.md`:

- **Tenant = Company** (TenantId = CompanyId)
- **Single database, shared schema** — логическая изоляция через row-level фильтры
- **Централизованные query filters** в EF Core
- **Fail-fast** при отсутствии tenant
- **System context** для миграций/фоновых задач

## Компоненты

### ITenantProvider

Единый интерфейс для получения TenantId текущего запроса/операции.

```csharp
public interface ITenantProvider
{
    Guid? TenantId { get; }
    bool IsSystemContext { get; }
}
```

### Tenant Resolution Providers

1. **JwtTenantProvider** — извлекает TenantId из JWT claim `companyId`
2. **HeaderTenantProvider** — извлекает TenantId из HTTP header `X-Tenant-Id`
3. **SystemTenantProvider** — для миграций/фоновых задач (bypass filter)
4. **CompositeTenantProvider** — пробует JWT → Header (fail-fast если не найдено)

### TenantResolutionMiddleware

Middleware для разрешения tenant из HTTP запроса:
- Проверяет наличие tenant перед выполнением use case
- Возвращает 401, если tenant не определён (кроме публичных endpoints)
- Должен быть зарегистрирован **до** аутентификации

### FixonDbContext

Использует `ITenantProvider` для автоматического применения query filters:
- Все `TenantEntity` фильтруются по `CompanyId`
- `ActivatableTenantEntity` дополнительно фильтруются по `IsActive`
- System context bypass все фильтры

### TenantDataLeakProtection

Утилиты для защиты от утечек данных:
- `ValidateTenantAccess<TEntity>` — проверяет принадлежность entity текущему tenant
- `ValidateTenantId` — проверяет TenantId при создании/обновлении
- `ValidateTenantAccessBatch` — валидирует список entities

## Использование

### Регистрация в DI

```csharp
// В Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddTenancyWithPublicEndpoints(); // или AddTenancy()

// Middleware (до аутентификации)
app.UseMiddleware<TenantResolutionMiddleware>();
```

### Использование в Application Layer

```csharp
public class ContractService
{
    private readonly FixonDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ContractService(FixonDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<Contract> GetContractAsync(Guid contractId)
    {
        // Query filter автоматически применяется
        var contract = await _dbContext.Contracts
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
        {
            throw new NotFoundException();
        }

        // Дополнительная защита от утечек (опционально, но рекомендуется)
        TenantDataLeakProtection.ValidateTenantAccess(
            contract,
            _tenantProvider.TenantId,
            _tenantProvider.IsSystemContext);

        return contract;
    }
}
```

### System Context (миграции, фоновые задачи)

```csharp
// В фоновой задаче
var tenantProvider = new SystemTenantProvider();
var options = new DbContextOptionsBuilder<FixonDbContext>()
    .UseNpgsql(connectionString)
    .Options;

using var dbContext = new FixonDbContext(options, tenantProvider);
// Теперь можно обращаться к данным всех tenant'ов
```

## Безопасность

### Защита от утечек данных

1. **Query filters** — автоматически применяются ко всем запросам
2. **Fail-fast** — отсутствие tenant приводит к 401
3. **Валидация** — `TenantDataLeakProtection` для явных проверок
4. **System context** — только для миграций/фоновых задач, не для обычных запросов

### Edge Cases

- **Публичные endpoints** (`/health`, `/api/auth/login`) — не требуют tenant
- **UserRole** — фильтруется через навигацию на `User.CompanyId`
- **Role** — глобальная сущность, не фильтруется по tenant

## Тестирование

### Unit Tests

```csharp
// Mock ITenantProvider
var tenantProvider = new Mock<ITenantProvider>();
tenantProvider.Setup(x => x.TenantId).Returns(testTenantId);
tenantProvider.Setup(x => x.IsSystemContext).Returns(false);

var dbContext = new FixonDbContext(options, tenantProvider.Object);
```

### Integration Tests

```csharp
// Используйте SystemTenantProvider для тестов, требующих доступа ко всем данным
var systemProvider = new SystemTenantProvider();
var dbContext = new FixonDbContext(options, systemProvider);
```

