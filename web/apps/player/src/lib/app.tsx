"use client";
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { api, ApiError, tokenStore, type Account, type Balances } from "./api";
import { detectLocale, errorMessage, formatNumber, isLocale, translate, type Key, type Locale } from "./i18n";

type Toast = { id: number; kind: "ok" | "err"; text: string };
type Ctx = {
  ready: boolean; account: Account | null; balances: Balances | null; locale: Locale;
  t: (key: Key, vars?: Record<string, string | number>) => string; n: (v: number) => string;
  setLocale: (l: Locale) => void; signIn: (token: string, account: Account) => void; signOut: () => void;
  refresh: () => Promise<void>; setBalances: (b: Balances) => void;
  notify: (kind: "ok" | "err", text: string) => void; fail: (e: unknown) => void;
};
const AppCtx = createContext<Ctx | null>(null);
export const useApp = () => { const c = useContext(AppCtx); if (!c) throw new Error("AppProvider missing"); return c; };

const LOCALE_KEY = "anima.locale";

export function AppProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [account, setAccount] = useState<Account | null>(null);
  const [balances, setBalances] = useState<Balances | null>(null);
  const [locale, setLocaleState] = useState<Locale>("en");
  const [toasts, setToasts] = useState<Toast[]>([]);
  const seq = useRef(0);

  const applyLocale = useCallback((l: Locale) => { setLocaleState(l); document.documentElement.lang = l; try { localStorage.setItem(LOCALE_KEY, l); } catch { /* bộ nhớ bị chặn */ } }, []);

  const notify = useCallback((kind: "ok" | "err", text: string) => {
    const id = ++seq.current; setToasts((x) => [...x, { id, kind, text }]);
    setTimeout(() => setToasts((x) => x.filter((v) => v.id !== id)), 4500);
  }, []);

  const signOut = useCallback(() => { tokenStore.clear(); setAccount(null); setBalances(null); }, []);

  const refresh = useCallback(async () => {
    const [a, b] = await Promise.all([api.me(), api.wallet()]);
    setAccount(a); setBalances(b);
  }, []);

  // Khởi động: ngôn ngữ đã lưu hoặc theo trình duyệt; nếu có token thì nạp hồ sơ (ngôn ngữ theo tài khoản được ưu tiên, BR-I18N-01).
  useEffect(() => {
    let stored: string | null = null;
    try { stored = localStorage.getItem(LOCALE_KEY); } catch { /* bỏ qua */ }
    applyLocale(isLocale(stored) ? stored : detectLocale(navigator.language));
    (async () => {
      if (tokenStore.get()) {
        try { const [a, b] = await Promise.all([api.me(), api.wallet()]); setAccount(a); setBalances(b); if (isLocale(a.locale)) applyLocale(a.locale); }
        catch (e) { if (e instanceof ApiError && e.status === 401) tokenStore.clear(); }
      }
      setReady(true);
    })();
  }, [applyLocale]);

  const value = useMemo<Ctx>(() => ({
    ready, account, balances, locale,
    t: (key, vars) => translate(locale, key, vars), n: (v) => formatNumber(locale, v),
    setLocale: (l) => { applyLocale(l); if (tokenStore.get()) api.setLocale(l).catch(() => undefined); },
    signIn: (token, a) => { tokenStore.set(token); setAccount(a); if (isLocale(a.locale)) applyLocale(a.locale); api.wallet().then(setBalances).catch(() => undefined); },
    signOut, refresh, setBalances, notify,
    fail: (e) => {
      if (e instanceof ApiError && e.status === 401 && e.code === "UNAUTHORIZED") { signOut(); return; }
      notify("err", e instanceof ApiError ? errorMessage(locale, e.code) : translate(locale, "err.GENERIC"));
    },
  }), [ready, account, balances, locale, applyLocale, signOut, refresh, notify]);

  return (
    <AppCtx.Provider value={value}>
      {children}
      <div className="toasts" role="status" aria-live="polite">{toasts.map((x) => <div key={x.id} className={`toast ${x.kind}`}>{x.text}</div>)}</div>
    </AppCtx.Provider>
  );
}

/** Chạy một hành động bất đồng bộ, khóa nút khi đang chạy (chống bấm đúp) và báo lỗi theo mã. */
export function useAction() {
  const { fail } = useApp();
  const [busy, setBusy] = useState(false);
  const run = useCallback(async <T,>(fn: () => Promise<T>): Promise<T | undefined> => {
    if (busy) return undefined;
    setBusy(true);
    try { return await fn(); } catch (e) { fail(e); return undefined; } finally { setBusy(false); }
  }, [busy, fail]);
  return { busy, run };
}
