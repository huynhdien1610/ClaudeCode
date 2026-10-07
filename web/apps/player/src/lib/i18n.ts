import { en, vi, zhHans, zhHant, type Key } from "./dict.ts";

export type Locale = "vi" | "en" | "zh-Hans" | "zh-Hant";
export const LOCALES: { code: Locale; label: string }[] = [
  { code: "vi", label: "Tiếng Việt" }, { code: "en", label: "English" }, { code: "zh-Hans", label: "简体中文" }, { code: "zh-Hant", label: "繁體中文" },
];
const DICTS: Record<Locale, Record<Key, string>> = { vi, en, "zh-Hans": zhHans, "zh-Hant": zhHant };
export const NUMBER_LOCALE: Record<Locale, string> = { vi: "vi-VN", en: "en-US", "zh-Hans": "zh-CN", "zh-Hant": "zh-TW" };

/** BR-I18N-01: ngôn ngữ mặc định theo trình duyệt nếu thuộc 4 mã hỗ trợ, ngược lại en. */
export function detectLocale(lang: string | undefined | null): Locale {
  const l = (lang ?? "").toLowerCase();
  if (l.startsWith("vi")) return "vi";
  if (/^zh-(tw|hk|mo|hant)/.test(l)) return "zh-Hant";
  if (l.startsWith("zh")) return "zh-Hans";
  return "en";
}
export const isLocale = (v: unknown): v is Locale => LOCALES.some((l) => l.code === v);

export function translate(locale: Locale, key: Key, vars?: Record<string, string | number>): string {
  let s = DICTS[locale]?.[key] ?? en[key] ?? key;
  if (vars) for (const [k, v] of Object.entries(vars)) s = s.split(`{${k}}`).join(String(v));
  return s;
}

/** Thông điệp lỗi theo mã nghiệp vụ (BR-I18N-04: mã không đổi theo ngôn ngữ). */
export function errorMessage(locale: Locale, code: string): string {
  const key = `err.${code}` as Key;
  return key in en ? translate(locale, key) : translate(locale, "err.GENERIC");
}

export const formatNumber = (locale: Locale, n: number) => n.toLocaleString(NUMBER_LOCALE[locale]);
export type { Key };
