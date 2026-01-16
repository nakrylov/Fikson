# 11-claims.md

## 1. Назначение документа

Документ фиксирует юридически значимый жизненный цикл Claims/Disputes и требования к воспроизводимости.

Источник связанной спецификации:
- `01-domain-model.md` (сущности)
- `03-sla-rule-model.md` (SLA evaluation → violation)
- `06-claims.md` (детализация lifecycle и инвариантов)
- `07-penalties-financial-impact.md` (Penalty + Adjustment)
- `08-audit-trail.md` (audit-first)

---

## 2. Ключевые принципы

- Claim — юридически значимый объект, не “статус” у нарушения.
- `ContractVersionId` фиксируется при создании Claim и не меняется.
- `Claim.Amount` фиксируется при создании (исходная рассчитанная сумма).
- Финальный финансовый эффект отражается через `Penalty` и `PenaltyAdjustment`, а не через переписывание Claim.
- Решения (`ClaimDecision`) append-only.
- Оспаривания (`Dispute`) отдельной сущностью.

---

## 3. Запрещено (anti-patterns)

- удалять claims
- менять `ContractVersionId`
- upsert фактов и “тихо исправлять” импорт
- закрывать claim без решения (для состояний review/accept/reject)

