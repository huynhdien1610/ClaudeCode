"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useApp } from "@/lib/app";
import { LOCALES, type Locale } from "@/lib/i18n";

const NAV = [["/home", "nav.home"], ["/store", "nav.store"], ["/packs", "nav.packs"], ["/quests", "nav.quests"], ["/collection", "nav.collection"], ["/forge", "nav.forge"], ["/wallet", "nav.wallet"], ["/account", "nav.account"]] as const;

export function LanguageSelect() {
  const { locale, setLocale, t } = useApp();
  return (
    <label className="lang"><span className="sr">{t("common.language")}</span>
      <select value={locale} onChange={(e) => setLocale(e.target.value as Locale)} aria-label={t("common.language")}>{LOCALES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}</select>
    </label>
  );
}

/** Khung sau đăng nhập: chặn khi chưa đăng nhập, thanh điều hướng, số dư. */
export function Shell({ children }: { children: ReactNode }) {
  const { ready, account, balances, t, n, signOut } = useApp();
  const path = usePathname(); const router = useRouter();
  useEffect(() => { if (ready && !account) router.replace("/login"); }, [ready, account, router]);
  if (!ready || !account) return <div className="center muted">{t("common.loading")}</div>;
  return (
    <>
      <header className="topbar">
        <Link href="/home" className="logo">ANIMA</Link>
        <nav className="nav" aria-label="Main">{NAV.map(([href, key]) => <Link key={href} href={href} className={path === href ? "on" : ""}>{t(key)}</Link>)}</nav>
        <span className="spacer" />
        <span className="wallet-pill" data-testid="balances" title={t("nav.wallet")}><b className="cur coin">{n(balances?.coin ?? 0)}</b> <small>{t("common.coin")}</small> · <b className="cur gem">{n(balances?.gem ?? 0)}</b> <small>{t("common.gem")}</small></span>
        <LanguageSelect />
        <button className="btn ghost sm" onClick={() => { signOut(); router.replace("/login"); }}>{t("nav.logout")}</button>
      </header>
      <main className="content">{children}</main>
    </>
  );
}

export function PageTitle({ children, sub }: { children: ReactNode; sub?: ReactNode }) {
  return <div className="page-title"><h1>{children}</h1>{sub && <p className="muted">{sub}</p>}</div>;
}

export function Modal({ onClose, children, label }: { onClose: () => void; children: ReactNode; label: string }) {
  const { t } = useApp();
  useEffect(() => { const k = (e: KeyboardEvent) => e.key === "Escape" && onClose(); window.addEventListener("keydown", k); return () => window.removeEventListener("keydown", k); }, [onClose]);
  return (
    <div className="overlay" role="dialog" aria-modal="true" aria-label={label} onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="dialog">{children}<div className="row end"><button className="btn" onClick={onClose}>{t("common.close")}</button></div></div>
    </div>
  );
}
