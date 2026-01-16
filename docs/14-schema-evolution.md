# 14-schema-evolution.md

## 1. Назначение документа

Документ фиксирует стратегию эволюции схемы и доменной модели Fixon (multi-tenant, contract-driven) с упором на:
- backward compatibility
- zero/low-downtime изменения
- безопасность и воспроизводимость
- уважение к долгоживущим данным (“данные старее кода”)

---

## 2. Базовые принципы

- **Forward-compatible schema**: новая схема должна понимать старые данные
- **Expand → Backfill → Switch → Contract**: никаких “big bang”
- **Explicit versioning**: изменения осознаны, документированы и отслеживаемы
- **Reversibility**: откат возможен (Down миграции + feature flag)
- **History-first**: Facts immutable; Claims/Penalties/Decisions append-only

---

## 3. Типы изменений

### 3.1 Безопасные (online)

- добавление nullable колонок
- добавление новых таблиц
- добавление индексов (желательно CONCURRENTLY)
- добавление новых значений enum (без переупорядочивания)

### 3.2 Условно опасные (требуют плана)

- переименование колонок
- смена типа
- изменение смысла поля

Требуют:
- dual write / dual read
- feature flag
- backfill job (tenant-by-tenant)
- audit прогресса и ошибок

### 3.3 Опасные (запрещены напрямую)

- удаление колонок/таблиц “сразу”
- silent rewrite исторических данных
- пересчёт прошлого “по новой логике” без versioning

---

## 4. Migration pattern Fixon (Expand → Backfill → Switch → Contract)

### Phase 1 — Expand

- добавить новые поля/таблицы/индексы (желательно без блокировок)
- код начинает писать и в старое и в новое (dual write) при необходимости
- чтение остаётся из старого (или fallback)

### Phase 2 — Backfill

- отдельный background job/use case
- idempotent
- tenant-by-tenant
- audit прогресса (batch/correlation id)
- без влияния на доменную логику (use case вызывает domain, не наоборот)

### Phase 3 — Switch

- feature flag
- чтение из нового
- старое остаётся fallback на релиз/два

### Phase 4 — Contract

- cleanup миграция (удаление старого)
- удаление dual write/read
- обновление docs

---

## 5. EF Core / PostgreSQL guidelines (zero-downtime)

### 5.1 Code-first migrations

- только code-first migrations
- не править старые миграции задним числом (только новая миграция)
- каждая миграция: атомарна, с `Down` по возможности

### 5.2 Индексы CONCURRENTLY

Для больших таблиц индексы создаются `CONCURRENTLY`.
В EF Core это обычно делается через `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ...")` и миграция должна выполняться **вне транзакции** (план: отдельный release step).

### 5.3 Добавление NOT NULL

Паттерн:
- добавить nullable колонку
- backfill
- добавить constraint/NOT NULL в отдельной миграции

### 5.4 Переименование

Паттерн:
- добавить новую колонку
- dual write
- backfill
- switch reads
- contract (удалить старую)

---

## 6. Domain backward compatibility

- versioned доменные объекты (например, `ContractVersion` snapshot json)
- старые версии контрактов не “апгрейдятся”, а остаются историей
- новая логика фиксируется как новые записи (append-only), а не перезапись старых

---

## 7. Operational checklist

- миграция имеет план отката (Down + feature flag)
- есть метрики backfill (rows migrated, failures, lag)
- есть audit для migration/backfill прогресса (correlation id)
- миграции проверены на lock/длительность (staging)
- tenant-by-tenant backfill может быть paused/resumed

---

## 8. Anti-patterns

- `ALTER COLUMN` без staged плана
- удаление данных “потому что не используется”
- миграции в runtime без контроля
- silent rewrite (без audit trail)

