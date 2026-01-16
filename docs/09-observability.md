# 09-observability.md

## 1. Назначение документа

Документ фиксирует подход Fixon к observability и эксплуатационной диагностике.

Принцип: **observability не меняет поведение системы**, она делает его видимым.

---

## 2. Structured logging (обязательно)

Логи структурированные (JSON), tenant-aware и correlation-aware.

Обязательные поля в scope:
- `tenantId`
- `correlationId`
- `operation`
- `result` (через HTTP status / outcome)

Запрещено:
- PII (email, ФИО, телефоны)
- raw payload импортов в логах (raw хранится в БД как юридический факт)

---

## 3. Metrics (минимум)

Технические:
- request latency / error rate
- background job duration + failures
- DB readiness (через health checks)

Domain-aware (read-only):
- `fixon.import.*` (rows uploaded/validated/invalid/duplicate, facts committed)

---

## 4. Tracing

Distributed tracing включает `correlationId` и не содержит чувствительных данных.

---

## 5. Health checks

- `/health/live` — liveness
- `/health/ready` — readiness (DB reachable + migrations applied)

---

## 6. Security signals (audit + monitoring)

Security posture должен быть auditable:
- Authorization forbidden → audit record `EntityType=SecurityEvent`, `Action=Forbidden`
- Cross-tenant write attempts → fail fast (blocked by write-guard)

