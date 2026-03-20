import { ru } from './ru';
import { en } from './en';

/**
 * Developer rule:
 * - All new user-facing UI strings must be added to `src/i18n/*` dictionaries.
 * - React components/pages must import strings from `t` (no hardcoded UI text).
 * - No runtime locale switch yet; default locale is Russian (`ru`).
 */

type WidenStrings<T> = T extends string ? string : T extends object ? { [K in keyof T]: WidenStrings<T[K]> } : T;
export type Dictionary = WidenStrings<typeof ru>;
export type Locale = 'ru' | 'en';

export const locale: Locale = 'ru';

const dictionaries: Record<Locale, Dictionary> = {
  ru,
  en
};

export const t: Dictionary = dictionaries[locale];

