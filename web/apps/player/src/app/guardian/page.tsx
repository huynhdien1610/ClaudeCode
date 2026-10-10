"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { LanguageSelect } from "@/components/Shell";

/** Trang người giám hộ mở từ liên kết trong email — không cần đăng nhập. */
export default function GuardianPage() {
  const { t } = useApp(); const { busy, run } = useAction();
  const [token, setToken] = useState(""); const [done, setDone] = useState(false);
  useEffect(() => { setToken(new URLSearchParams(window.location.search).get("token") ?? ""); }, []);
  return (
    <main className="content center">
      <section className="panel grid" style={{ maxWidth: 480 }} data-testid="guardian-page">
        <div className="row"><h2>{t("guardian.title")}</h2><span className="spacer" /><LanguageSelect /></div>
        {done ? <p className="chip" data-testid="guardian-ok">✓ {t("guardian.done")}</p> : (<>
          <p>{t("guardian.text")}</p>
          <button className="btn primary" disabled={busy || !token} data-testid="guardian-confirm" onClick={() => run(async () => { await api.confirmGuardian(token); setDone(true); })}>{t("guardian.confirm")}</button>
        </>)}
      </section>
    </main>
  );
}
