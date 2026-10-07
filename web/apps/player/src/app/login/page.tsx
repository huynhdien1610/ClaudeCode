"use client";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { api } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { LanguageSelect } from "@/components/Shell";

const COUNTRIES = ["VN", "SG", "MY", "PH", "TH", "ID", "TW", "HK", "US", "GB", "JP", "KR", "AU"];

export default function LoginPage() {
  const { ready, account, locale, t, signIn } = useApp(); const router = useRouter();
  const { busy, run } = useAction();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [f, setF] = useState({ email: "", password: "", birthDate: "", country: "VN" });
  useEffect(() => { if (ready && account) router.replace("/home"); }, [ready, account, router]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => {
      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";   // múi giờ cố định theo tài khoản khi đăng ký (BR-CHK-01)
      const r = mode === "login" ? await api.login({ email: f.email, password: f.password }) : await api.register({ ...f, locale, timezone: tz });
      signIn(r.accessToken, r.account); router.replace("/home");
    });
  };

  return (
    <div className="auth">
      <div className="logo">ANIMA</div>
      <p className="muted" style={{ textAlign: "center" }}>{t("app.tagline")}</p>
      <form className="panel grid" onSubmit={submit} aria-label={mode === "login" ? t("auth.login") : t("auth.register")}>
        <h2>{mode === "login" ? t("auth.login") : t("auth.register")}</h2>
        <label>{t("auth.email")}<input type="email" autoComplete="email" required value={f.email} onChange={(e) => setF({ ...f, email: e.target.value })} data-testid="email" /></label>
        <label>{t("auth.password")}<input type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} required minLength={8} value={f.password} onChange={(e) => setF({ ...f, password: e.target.value })} data-testid="password" /></label>
        {mode === "register" && (<>
          <label>{t("auth.birth")}<input type="date" required value={f.birthDate} onChange={(e) => setF({ ...f, birthDate: e.target.value })} data-testid="birth" /></label>
          <label>{t("auth.country")}<select value={f.country} onChange={(e) => setF({ ...f, country: e.target.value })} data-testid="country">{COUNTRIES.map((c) => <option key={c}>{c}</option>)}</select></label>
          <div className="note">{t("auth.newbieGift")}</div>
        </>)}
        <button className="btn primary" disabled={busy} data-testid="submit">{mode === "login" ? t("auth.login") : t("auth.register")}</button>
        <button type="button" className="btn ghost" onClick={() => setMode(mode === "login" ? "register" : "login")} data-testid="switch">{mode === "login" ? t("auth.toRegister") : t("auth.toLogin")}</button>
      </form>
      <div className="row"><LanguageSelect /><span className="spacer" /><small className="muted">{t("auth.devNote")}</small></div>
    </div>
  );
}
