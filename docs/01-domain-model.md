# Fixon — Domain Model

## 1. Общие принципы доменной модели

Доменная модель Fixon строится вокруг следующих принципов:

- Явные и простые сущности
- Минимальное количество связей
- Отсутствие «умных» сущностей
- Полная аудитируемость действий
- Поддержка мультикомпаний (multi-tenant SaaS)

---

## 2. Мультикомпанийная модель (Multi-tenancy)

### Company
Компания — верхнеуровневая сущность-арендатор (tenant).

**Ответственность:**
- Изоляция данных
- Владение пользователями, контрактами, правилами и фактами

**Ключевые поля:**
- Id
- Name
- CreatedAt
- IsActive

---

## 3. Пользователи и доступ

### User
Пользователь системы, принадлежащий компании.

**Ответственность:**
- Идентификация и авторизация
- Авторство изменений
- Аудит действий

**Ключевые поля:**
- Id
- CompanyId
- Email
- Name
- PasswordHash
- IsActive
- CreatedAt

---

### Role
Роль пользователя в компании.

Примеры:
- Admin
- Manager
- Viewer

**Ключевые поля:**
- Id
- Name

---

### UserRole
Связь many-to-many между User и Role.

**Ключевые поля:**
- UserId
- RoleId

---

## 4. Контрагенты

### Counterparty
Контрагент по договору.

**Ответственность:**
- Представление второй стороны договора

**Ключевые поля:**
- Id
- CompanyId
- Name
- ExternalCode (опционально)
- IsActive

---

## 5. Контракты и версии

### Contract
Логический контракт между Company и Counterparty.

**Ответственность:**
- Группировка версий договора

**Ключевые поля:**
- Id
- CompanyId
- CounterpartyId
- Name
- IsActive
- CreatedAt

---

### ContractVersion
Конкретная версия контракта.

**Ответственность:**
- Хранение актуального состояния договора
- Привязка SLA-правил

**Ключевые поля:**
- Id
- CompanyId
- ContractId
- VersionNumber
- EffectiveFrom
- EffectiveTo (nullable)
- PdfFilePath (опционально)
- CreatedAt
- CreatedByUserId
- IsActive

---

## 6. SLA правила и версии

### SlaRule
Логическое SLA-правило (бизнес-сущность).

**Ответственность:**
- Группировка версий правила

**Ключевые поля:**
- Id
- CompanyId
- ContractId
- Name
- IsActive
- CreatedAt

---

### SlaRuleVersion
Версия SLA-правила, используемая для вычислений.

**Ответственность:**
- Хранение условий применимости
- Хранение логики проверки SLA
- Определение последствий нарушения

**Ключевые поля:**
- Id
- CompanyId
- SlaRuleId
- VersionNumber
- AppliesWhenJson
- ConditionJson
- PenaltyJson
- EffectiveFrom
- EffectiveTo (nullable)
- CreatedAt
- CreatedByUserId
- IsActive

> `AppliesWhenJson` — условия применимости правила  
> `ConditionJson` — условие проверки SLA  
> `PenaltyJson` — описание последствий

---

## 7. Факты исполнения

### Fact
Факт — атомарное событие или измерение, используемое для проверки SLA.

**Ответственность:**
- Хранение фактических данных
- Универсальная структура для любых типов SLA

**Ключевые поля:**
- Id
- CompanyId
- ContractId (nullable)
- ExternalReference (опционально)
- FactType
- AttributesJson
- OccurredAt
- CreatedAt

> `AttributesJson` — набор параметров факта (key-value)

---

## 8. Результаты проверки SLA

### SlaEvaluation
Результат проверки одного SLA-правила по одному или нескольким фактам.

**Ответственность:**
- Фиксация результата вычисления
- Аудит применённых правил и фактов

**Ключевые поля:**
- Id
- CompanyId
- ContractVersionId
- SlaRuleVersionId
- EvaluationResult (Pass / Fail)
- CalculatedValuesJson
- EvaluatedAt

---

## 9. Claims / отклонения

### Claim
Зафиксированное отклонение по результатам SLA.

**Ответственность:**
- Представление бизнес-факта отклонения
- Финансовая и операционная фиксация

**Ключевые поля:**
- Id
- CompanyId
- ContractId
- ContractVersionId
- SlaRuleVersionId
- SlaViolationId
- Status (Draft / Submitted / UnderReview / Accepted / Rejected / Disputed / Resolved / Cancelled)
- Amount  
  Исходная рассчитанная сумма Claim (фиксируется при создании; финальный финансовый эффект отражается через Penalty + PenaltyAdjustment, см. `07-penalties-financial-impact.md`).
- Currency  
  Валюта исходной рассчитанной суммы Claim. Конвертация валют в текущей версии не выполняется.
- OpenedAt
- ClosedAt (nullable)
- CreatedByUserId

---

### SlaViolation
Immutable факт нарушения SLA (на основе SLA evaluation), используемый как юридическое основание для Claim.

---

### ClaimDecision
Append-only решение по Claim (Accept / Reject / PartialAccept) (см. `06-claims.md`).

---

### Dispute
Оспаривание Claim (переговорный процесс), отдельный агрегат (см. `06-claims.md`).

---

### Penalty
Денежное последствие нарушения SLA, связанное с Claim (см. `07-penalties-financial-impact.md`).

Инвариант:
- один Claim имеет ровно один Penalty

---

### PenaltyAdjustment
Ручная корректировка (override/adjustment) рассчитанного штрафа (см. `07-penalties-financial-impact.md`).

Инварианты:
- исходный рассчитанный Penalty не изменяется
- итоговая сумма определяется последней корректировкой

---

## 10. Аудит и история изменений

### AuditLog
Журнал действий пользователей.

**Ответственность:**
- Трассировка изменений
- Юридическая и операционная прозрачность

**Ключевые поля:**
- Id
- CompanyId
- ActorType
- ActorUserId (nullable)
- ActorSystem (nullable)
- EntityType
- EntityId
- Action
- CorrelationId (nullable)
- PreviousAuditLogId (nullable)
- DetailsJson
- Timestamp

---

## 11. Imports / загрузка данных

### ImportBatch
Партия импорта (CSV/API/Manual). Двухфазный процесс: validate → commit (см. `05-import-csv.md`).

**Ключевые поля:**
- Id
- CompanyId
- Source (Csv / Api / Manual)
- ExternalBatchId (nullable)
- Status (Uploaded / Validated / Committed / Failed)
- Checksum
- StartedAt
- FinishedAt (nullable)

### ImportRow
Строка импорта с сохранением raw payload и результатом валидации.

**Ключевые поля:**
- Id
- CompanyId
- ImportBatchId
- RowNumber
- RawPayload
- ValidationStatus (Pending / Valid / Invalid / Duplicate)
- ErrorCode (nullable)
- ErrorMessage (nullable)

---

## 12. Связи между сущностями (кратко)

- Company → Users
- Company → Contracts
- Contract → ContractVersions
- Contract → SlaRules
- SlaRule → SlaRuleVersions
- ContractVersion → SlaEvaluations
- SlaRuleVersion → SlaEvaluations
- SlaEvaluation → Claim
- Company → Facts
- User → AuditLog

---

## 13. Ключевые инварианты домена

- Все сущности принадлежат Company
- Расчёты всегда выполняются по версиям
- Старые версии никогда не изменяются
- Любой расчёт должен быть воспроизводим
- PDF никогда не используется как источник данных

---

## 14. Осознанные упрощения (v1)

- Нет иерархий ролей
- Нет сложных permission-моделей
- Нет soft-delete для всех сущностей
- Нет кросс-контрактных SLA
- Нет ретроактивного перерасчёта

---

## 14. Доменные термины (глоссарий)

- **Fact** — зафиксированное событие или измерение
- **SLA Rule** — формализованное условие договора
- **Evaluation** — результат проверки SLA
- **Claim** — бизнес-факт отклонения
