# 13-security.md

## 1. Назначение документа

Документ фиксирует security posture Fixon для multi-tenant SaaS:
- гарантии tenant isolation
- defense-in-depth (API → Application → EF → DB)
- auditable security events

Принцип: **Tenant isolation — свойство системы**, а не “фича”.

---

## 2. Threat model (упрощённо)

- ошибка разработчика (забыли фильтр/tenant guard)
- подмена идентификаторов (IDOR)
- скомпрометированный интеграционный клиент (3PL/API)
- массовые операции (imports) как DoS

---

## 3. Defense layers (Fixon)

### 3.1 Identity & access

- JWT обязателен для пользовательских запросов
- TenantId берётся **только** из `tenant_id` claim
- роли/permissions проверяются policy-based authorization

Запрещено:
- принимать TenantId из body
- вычислять tenant из URL

### 3.2 Tenant resolution (API)

Tenant resolution:
- основной источник: JWT claim `tenant_id`
- интеграционный fallback: `X-Api-Key` + `X-Tenant-Id` (только если включено конфигом)

Secure-by-default:
- `X-Tenant-Id` без API ключа не используется
- для authenticated запросов `X-Tenant-Id` игнорируется

### 3.3 EF Core hard isolation (read + write)

- Global query filters по `CompanyId` для tenant-scoped сущностей
- Write-side guard: запрет cross-tenant writes на `SaveChanges`

### 3.4 Database constraints

- уникальные индексы и FK всегда tenant-scoped где применимо (например, idempotency keys)
- delete behavior: Restrict (без каскадов по умолчанию)

Опционально (будущее):
- PostgreSQL RLS для дополнительного уровня защиты

### 3.5 Security events (audit)

События безопасности — часть audit trail (append-only), без PII:
- authorization forbidden (403)
- cross-tenant write blocked
- admin/system overrides (если появятся)

---

## 4. Anti-patterns (строго запрещены)

- optional TenantId в tenant-scoped данных
- IgnoreQueryFilters() в application коде
- “god mode” админа без audit trail
- upsert фактов как “исправление импорта”

