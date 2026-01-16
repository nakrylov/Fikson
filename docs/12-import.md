# 12-import.md

## 1. Назначение документа

Документ фиксирует требования к импорту (ingestion) как к **юридически значимому факту**: raw данные сохраняются, обработка воспроизводима, повторная загрузка не приводит к повторному созданию фактов без следа.

Источник для реализации CSV v1/v2: `05-import-csv.md`.

---

## 2. Принципы (Fixon)

- Two-phase import: **Upload → Validate → Commit**
- Raw payload preservation: `ImportRow.RawPayload` неизменяем
- Append-only import tables: `ImportBatch` и `ImportRow` не удаляются
- Idempotency by design для domain facts: уникальный ключ на уровне БД (tenant-scoped)
- No upsert: факт не обновляется “тихо”; конфликт фиксируется как диагностический результат

---

## 3. Связь с доменом

- SLA engine читает только committed facts
- import **не триггерит пересчёт напрямую**, пересчёт выполняется отдельными job/use cases
- Audit фиксирует: Upload/Validate/Commit/Fail и итоговые счётчики

