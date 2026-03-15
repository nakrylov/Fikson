# Fixon Frontend (SPA)

## i18n convention (lightweight)

- Dictionaries are in `src/i18n/` (`ru.ts` is the current default locale).
- Import UI strings from `t`:

```ts
import { t } from './i18n';
```

- Do not hardcode user-visible strings directly in React components/pages.

