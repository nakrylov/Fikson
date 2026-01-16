# Fixon — CSV Import Model

## 1. Назначение документа

Документ описывает:
- принципы импорта данных из CSV,
- поддерживаемые сценарии,
- структуру файлов,
- логику обработки и валидации,
- связь импортированных данных с доменной моделью.

Импорт CSV является ключевым способом загрузки фактов и справочных данных в систему.

---

## 2. Общие принципы импорта

1. CSV — основной формат для v1
2. Один CSV файл — один тип сущности
3. Импорт выполняется пользователем вручную
4. Импорт всегда выполняется в контексте компании
5. Все строки валидируются
6. Ошибки не прерывают весь импорт

---

## 3. Типы поддерживаемых импортов

### Поддерживаемые типы CSV:

- Contracts
- ContractVersions
- SLA Rules
- SLA Rule When Conditions
- SLA Rule Then Conditions
- Shipments
- Facts

Каждый тип импортируется отдельно.

---

## 4. Импорт контрактов

### 4.1 Назначение

Импорт контрактов используется для:
- начальной загрузки данных
- миграции из других систем

---

### 4.2 Пример структуры CSV

Колонки:
- ContractCode
- ContractName
- PartnerName
- StartDate
- EndDate

---

### 4.3 Правила обработки

- ContractCode уникален в рамках компании
- При конфликте:
  - либо ошибка
  - либо обновление (опция в будущем)

---

## 5. Импорт версий контрактов

### 5.1 Пример структуры CSV

Колонки:
- ContractCode
- VersionNumber
- EffectiveFrom
- EffectiveTo
- Status

---

### 5.2 Правила

- ContractCode должен существовать
- VersionNumber уникален в рамках контракта
- Только одна версия может быть Active

---

## 6. Импорт SLA Rules

### 6.1 Назначение

Импорт SLA правил позволяет:
- массово загружать правила
- формализовать договоры

---

### 6.2 Пример структуры CSV

Колонки:
- ContractCode
- ContractVersion
- RuleCode
- RuleName
- Severity
- IsActive
- Description

---

### 6.3 Правила обработки

- RuleCode уникален в рамках версии контракта
- Правило создаётся без условий
- Условия импортируются отдельно

---

## 7. Импорт When Conditions

### 7.1 Назначение

When Conditions определяют применимость правила.

---

### 7.2 Пример структуры CSV

Колонки:
- ContractCode
- ContractVersion
- RuleCode
- Attribute
- Operator
- Value

---

### 7.3 Примеры

- cargo.category | EQUALS | Vegetables
- cargo.category | IN | Vegetables,Fruits

---

### 7.4 Правила

- RuleCode должен существовать
- Operator валидируется по справочнику
- Все условия объединяются через AND

---

## 8. Импорт Then Conditions

### 8.1 Назначение

Then Conditions определяют проверку SLA.

---

### 8.2 Пример структуры CSV

Колонки:
- ContractCode
- ContractVersion
- RuleCode
- FactKey
- Operator
- ExpectedValue
- Unit

---

### 8.3 Примеры

- waiting_time_minutes | LESS_OR_EQUALS | 30 | minutes
- cargo_temperature | GREATER_OR_EQUALS | 2 | celsius
- cargo_temperature | LESS_OR_EQUALS | 6 | celsius

---

## 9. Импорт Shipments

### 9.1 Назначение

Shipment — контейнер фактов.

---

### 9.2 Пример структуры CSV

Колонки:
- ShipmentExternalId
- ContractCode
- ContractVersion
- ShipmentDate
- CargoCategory
- CargoType
- RouteFrom
- RouteTo

---

### 9.3 Правила

- ShipmentExternalId уникален в рамках компании
- ContractVersion должна быть Active или Historical

---

## 10. Импорт Facts

### 10.1 Назначение

Facts — атомарные измерения для проверки SLA.

---

### 10.2 Гибкая модель фактов

Facts хранятся как:
- FactKey
- FactValue
- RecordedAt
- Source

FactValue хранится как string и интерпретируется при вычислении.

---

### 10.3 Пример структуры CSV

Колонки:
- ShipmentExternalId
- FactKey
- FactValue
- RecordedAt
- Source

---

### 10.4 Примеры FactKey

- arrival_time
- waiting_time_minutes
- cargo_temperature
- documents.excise_stamp_present

---

## 11. Валидация при импорте

Валидации:
- обязательные поля
- корректность операторов
- наличие связей
- тип данных (мягкая проверка)

Ошибки:
- фиксируются построчно
- не прерывают импорт
- доступны пользователю в отчёте

---

## 12. Отчёт об импорте

По завершении импорта создаётся ImportReport:

- ImportType
- FileName
- TotalRows
- SuccessRows
- FailedRows
- Errors

---

## 13. Идемпотентность

### 13.1 v1

В v1 повторный импорт того же файла:
- создаёт дубликаты
- контроль дубликатов — ответственность пользователя

### 13.2 v2 (audit-first, replay-safe)

В v2 импорт является юридически значимым фактом и должен быть воспроизводим и идемпотентен.

Принципы:
- Two-phase import: **validate → commit**
- Raw data preservation: исходные строки сохраняются неизменно
- Повторная загрузка ≠ повторная обработка: повторный commit невозможен

Idempotency key для фактов:
- `CompanyId + ExternalReference + OccurredAt`

Техническая реализация:
- уникальный индекс в БД на `(CompanyId, ExternalReference, OccurredAt)` (partial: `ExternalReference is not null`)
- хранение `PayloadHash` (hash raw payload) для диагностики конфликтов
- факт не обновляется (“no upsert”), при конфликте фиксируется duplicate и строка остаётся в ImportRow как диагностическая запись

---

## 14. Ограничения v1

- Только CSV
- Нет Excel
- Нет API импорта
- Нет drag & drop
- Нет трансформаций

---

## 15. Осознанные архитектурные решения

- Плоский CSV проще сложных форматов
- Явная структура лучше универсальной
- Импорт разделён по сущностям
- Простота важнее автоматизации

---

## 16. Готовность к реализации

Модель импорта:
- согласована с ER
- поддерживает сложные правила
- не требует ETL
- легко расширяется
