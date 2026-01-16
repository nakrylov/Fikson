# Fixon — ER Diagram (Textual)

Документ описывает ER-модель системы Fixon в текстовом виде.
Используется как источник для генерации:
- EF Core entities
- DbContext
- Migrations

---

## 1. Company


Company

Id (PK)
Name
CreatedAt
IsActive


---

## 2. User


User

Id (PK)
CompanyId (FK → Company.Id)
Email
Name
PasswordHash
IsActive
CreatedAt


---

## 3. Role


Role

Id (PK)
Name


---

## 4. UserRole


UserRole

UserId (PK, FK → User.Id)
RoleId (PK, FK → Role.Id)


---

## 5. Counterparty


Counterparty

Id (PK)
CompanyId (FK → Company.Id)
Name
ExternalCode (nullable)
IsActive


---

## 6. Contract


Contract

Id (PK)
CompanyId (FK → Company.Id)
CounterpartyId (FK → Counterparty.Id)
Name
IsActive
CreatedAt


---

## 7. ContractVersion


ContractVersion

Id (PK)
CompanyId (FK → Company.Id)
ContractId (FK → Contract.Id)
VersionNumber
EffectiveFrom
EffectiveTo (nullable)
PdfFilePath (nullable)
CreatedAt
CreatedByUserId (FK → User.Id)
IsActive


---

## 8. SlaRule


SlaRule

Id (PK)
CompanyId (FK → Company.Id)
ContractId (FK → Contract.Id)
Name
IsActive
CreatedAt


---

## 9. SlaRuleVersion


SlaRuleVersion

Id (PK)
CompanyId (FK → Company.Id)
SlaRuleId (FK → SlaRule.Id)
VersionNumber
AppliesWhenJson
ConditionJson
PenaltyJson
EffectiveFrom
EffectiveTo (nullable)
CreatedAt
CreatedByUserId (FK → User.Id)
IsActive


---

## 10. Fact


Fact

Id (PK)
CompanyId (FK → Company.Id)
ContractId (FK → Contract.Id, nullable)
ExternalReference (nullable)
FactType
AttributesJson
OccurredAt
CreatedAt


---

## 11. SlaEvaluation


SlaEvaluation

Id (PK)
CompanyId (FK → Company.Id)
ContractVersionId (FK → ContractVersion.Id)
SlaRuleVersionId (FK → SlaRuleVersion.Id)
EvaluationResult
CalculatedValuesJson
EvaluatedAt


---

## 12. Claim


Claim

Id (PK)
CompanyId (FK → Company.Id)
ContractId (FK → Contract.Id)
ContractVersionId (FK → ContractVersion.Id)
SlaRuleVersionId (FK → SlaRuleVersion.Id)
SlaViolationId (FK → SlaViolation.Id)
Status
Amount
Currency
OpenedAt
ClosedAt (nullable)
CreatedByUserId (FK → User.Id)

---

## 12.0 SlaViolation

SlaViolation

Id (PK)
CompanyId (FK → Company.Id)
ContractVersionId (FK → ContractVersion.Id)
SlaRuleVersionId (FK → SlaRuleVersion.Id)
SlaEvaluationId (FK → SlaEvaluation.Id)
CalculatedValuesJson
DetectedAt

---

## 12.0.1 ClaimDecision

ClaimDecision

Id (PK)
CompanyId (FK → Company.Id)
ClaimId (FK → Claim.Id)
DecisionType
DecisionAmount
Currency
DecidedByUserId (FK → User.Id)
DecidedAt
Comment (nullable)

---

## 12.0.2 Dispute

Dispute

Id (PK)
CompanyId (FK → Company.Id)
ClaimId (FK → Claim.Id)
InitiatedBy
Reason
Status
OpenedAt
ResolvedAt (nullable)

---

## 12.1 Penalty

Penalty

ClaimId (PK, FK → Claim.Id)
CompanyId (FK → Company.Id)
CalculatedAmount
Currency
CalculatedAt
PenaltyJsonSnapshot
ExplanationJson

---

## 12.2 PenaltyAdjustment

PenaltyAdjustment

Id (PK)
ClaimId (FK → Claim.Id)
CompanyId (FK → Company.Id)
OriginalPenaltyAmount
NewPenaltyAmount
Currency
Reason
AdjustedByUserId (FK → User.Id)
AdjustedAt


---

## 13. AuditLog


AuditLog

Id (PK)
CompanyId (FK → Company.Id)
UserId (FK → User.Id)
EntityType
EntityId
ActionType
ChangesJson

---

## 14. ImportBatch

ImportBatch

Id (PK)
CompanyId (FK → Company.Id)
Source
ExternalBatchId (nullable)
Status
Checksum
StartedAt
FinishedAt (nullable)

---

## 15. ImportRow

ImportRow

Id (PK)
CompanyId (FK → Company.Id)
ImportBatchId (FK → ImportBatch.Id)
RowNumber
RawPayload
ValidationStatus
ErrorCode (nullable)
ErrorMessage (nullable)
CreatedAt


---

## 14. Основные связи (summary)



Company
├── Users
├── Counterparties
├── Contracts
├── Facts
├── Claims
└── AuditLogs

User
├── UserRoles
├── ContractVersions (CreatedBy)
├── SlaRuleVersions (CreatedBy)
├── Claims (CreatedBy)
└── AuditLogs

Contract
├── ContractVersions
└── SlaRules

SlaRule
└── SlaRuleVersions

ContractVersion
├── SlaEvaluations
└── Claims

SlaRuleVersion
├── SlaEvaluations
└── Claims

SlaEvaluation
└── Claim (optional)


---

## 15. Принципы моделирования

- Все таблицы имеют явный PK
- Все бизнес-сущности привязаны к Company
- Версии неизменяемы после создания
- JSON-поля используются осознанно для гибкости SLA
- Отсутствуют циклические зависимости

---

## 16. Готовность к реализации

Данная ER-модель:
- полностью согласована с `01-domain-model.md`
- оптимальна для EF Core + PostgreSQL
- подходит для генерации миграций
- допускает эволюцию без ломки схемы