# 10-contract-versioning.md

## 1. Назначение документа

Документ фиксирует механизм версирования контрактов в Fixon.

Принцип: **контракт — юридический источник истины**. Подписанная версия контракта неизменяема и используется для детерминированного пересчёта SLA/penalties.

---

## 2. Доменная модель

### 2.1 Contract

- ContractId
- CompanyId (TenantId)
- Status: Draft | Active | Superseded | Terminated
- CurrentVersionId (nullable)

Инварианты:
- нельзя иметь две активные версии одновременно
- нельзя активировать версию для terminated контракта

### 2.2 ContractVersion

- ContractVersionId
- ContractId
- VersionNumber
- EffectiveFrom
- EffectiveTo (nullable)
- Status: Draft | Signed | Archived
- CreatedAt
- SignedAt (nullable)
- Snapshot:
  - SlaRulesSnapshot (immutable json)
  - PenaltyRulesSnapshot (immutable json)

Инварианты:
- `Signed` версия неизменяема
- `Snapshot` хранится как полное состояние (не diff)
- версии не удаляются

---

## 3. Lifecycle (text)

1) Contract создан → Version 1 создаётся как Draft  
2) Draft версия подписывается → Signed  
3) Signed версия активируется → Contract становится Active, CurrentVersionId указывает на версию  
4) Изменения → создаётся новая Draft версия (v2)  
5) v2 подписывается и активируется → v1 становится Archived (историческая)  
6) TerminateContract → контракт Terminated, пересчёты не используют новые версии

---

## 4. Правила выбора версии (deterministic replay)

### 4.1 SLA evaluation

`SlaEvaluation` выполняется по **конкретной** `ContractVersionId`, определённой на момент события/факта.

### 4.2 Claim / Penalty

- Claim всегда содержит `ContractVersionId` (и не меняется)
- Penalty рассчитывается по нарушениям, зафиксированным по этой же версии

### 4.3 Запрещено

- “берём текущую активную”
- выбор по `DateTime.Now`
- implicit version selection

---

## 5. Storage strategy

Минимально (v1):
- `ContractVersions` — отдельная таблица
- snapshot хранится в jsonb (`SlaRulesSnapshot`, `PenaltyRulesSnapshot`)

Плюсы:
- неизменяемость и воспроизводимость
- простая миграция данных

Минусы:
- сложнее точечный поиск внутри snapshot (компенсируется read-моделями/отчётами)

