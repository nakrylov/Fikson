# 08-audit-trail.md

## 1. Назначение документа

Документ фиксирует audit trail (не logging) в Fixon.

Принципы:
- audit trail — append-only
- audit записи не изменяются и не “исправляются”
- любая корректировка — новая запись, с ссылкой на предыдущую
- Domain не пишет audit напрямую: инициирует Application, сохраняет Infrastructure

---

## 2. Что аудитируем (минимум)

Обязательно фиксировать:
- Imports (CSV): start/success/failure + summary
- SLA evaluations: created
- Claims: created/status changes
- Penalty calculations: calculated (amount + explanation snapshot)
- Penalty overrides: adjustments
- Manual actions пользователей: contract/version lifecycle, confirmations
- Background jobs: start/success/failure (для истории пересчётов)

---

## 3. AuditLog (модель)

Минимальные поля:
- AuditId
- CompanyId (TenantId)
- EntityType
- EntityId
- Action
- Timestamp (UTC)
- ActorType: User | System
- ActorUserId (nullable)
- ActorSystem (nullable)
- CorrelationId (nullable)
- PreviousAuditLogId (nullable)
- DetailsJson (jsonb: snapshot/diff/details)

Ограничения:
- без PII
- DetailsJson должен быть валидным JSON

