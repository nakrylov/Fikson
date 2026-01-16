# 06-claims.md

## 1. Назначение документа

Документ фиксирует модель **Claims & Disputes** (претензий/оспариваний) в Fixon и связь с:
- `03-sla-rule-model.md` (оценка SLA)
- `07-penalties-financial-impact.md` (денежные последствия)
- `08-audit-trail.md` (audit trail)
- `10-contract-versioning.md` (версии контракта)

Принцип: **Claim — юридически значимый объект**, а не просто статус у SLA violation.

---

## 2. Связь сущностей

Цепочка:

Facts → SLA RuleVersion → SLA Evaluation → SLA Violation → Claim → Penalty

Инварианты:
- `SlaEvaluation` создаётся на основе фактов и версии правил
- `SlaViolation` — immutable факт нарушения (на базе evaluation)
- `Claim` создаётся только при наличии `SlaViolation`
- `Claim.ContractVersionId` фиксируется при создании и не меняется
- если создан `Claim`, то для него создаётся `Penalty` (может быть 0)

---

## 3. Claim (модель)

### 3.1 Поля (v2)

- Id
- CompanyId (TenantId)
- ContractId
- ContractVersionId (версия контракта, используемая для этого отклонения)
- SlaViolationId (обязателен)
- Status: Draft | Submitted | UnderReview | Accepted | Rejected | Disputed | Resolved | Cancelled
- ClaimAmount (фиксируется при создании; "исходная рассчитанная сумма")
- Currency (ISO 4217, конвертация в v1 не выполняется)
- OpenedAt
- ClosedAt (nullable)
- CreatedByUserId

Запрещено:
- редактировать ClaimAmount после `Submitted`
- менять `ContractVersionId`
- удалять claim

### 3.2 ClaimStatus (state machine)

Правила переходов:
- Draft → Submitted (только tenant)
- Submitted → UnderReview
- UnderReview → Accepted | Rejected (решение)
- Accepted → Disputed (инициатор: 3PL)
- Disputed → Resolved
- Any → Cancelled (только до решения)

Примечание:
- Частичное принятие моделируется **решением** (см. `ClaimDecision`) и не является "пересчётом SLA".

## 4. Dispute (оспаривание)

Dispute — отдельный процесс переговоров, привязанный к claim.

Поля:
- Id
- ClaimId
- InitiatedBy: Tenant | ThreePL
- Reason
- Status: Open | Resolved
- OpenedAt
- ResolvedAt (nullable)

Инварианты:
- нельзя спорить закрытый claim
- dispute не изменяет историю SLA; влияет только на финальный финансовый эффект через решение/adjustment

---

## 5. ClaimDecision (решение)

Decision — append-only запись решения по claim.

Поля:
- Id
- ClaimId
- DecisionType: Accept | Reject | PartialAccept
- DecisionAmount
- Currency
- DecidedByUserId
- DecidedAt
- Comment (optional)

Инварианты:
- нельзя принять/отклонить claim без решения
- решения не пересчитывают SLA
- финальный финансовый эффект фиксируется через `PenaltyAdjustment` (см. `07-penalties-financial-impact.md`)

## 6. Audit

Все ручные действия по Claim/Dispute/Decision аудитируются (см. `08-audit-trail.md`).

