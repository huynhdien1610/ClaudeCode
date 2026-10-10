"use client";
import { useCallback, useEffect, useState } from "react";
import { api, type RevealedSeed, type SeedPublic, type VerifyResult } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle, LanguageSelect } from "@/components/Shell";

export default function AccountPage() {
  const { t, account, refresh, notify, fail } = useApp(); const { busy, run } = useAction();
  const [guardian, setGuardian] = useState(""); const [guardianSent, setGuardianSent] = useState(false);
  const [phone, setPhone] = useState(""); const [code, setCode] = useState(""); const [sent, setSent] = useState(false);
  const [seed, setSeed] = useState<SeedPublic | null>(null); const [client, setClient] = useState("");
  const [revealed, setRevealed] = useState<RevealedSeed | null>(null); const [history, setHistory] = useState<RevealedSeed[]>([]);
  const [tool, setTool] = useState({ serverSeed: "", clientSeed: "", nonce: 1, slots: 5 }); const [verify, setVerify] = useState<VerifyResult | null>(null);

  const load = useCallback(() => Promise.all([api.fairness(), api.fairnessHistory()]).then(([s, h]) => { setSeed(s); setClient(s.clientSeed); setHistory(h); }).catch(fail), [fail]);
  useEffect(() => { load(); }, [load]);

  return (
    <>
      <PageTitle>{t("account.title")}</PageTitle>
      <div className="cols">
        <section className="panel grid">
          <div className="row"><h3>{t("common.language")}</h3><span className="spacer" /><LanguageSelect /></div>
          <div className="row wrap"><span className="chip" data-testid="status">{account?.status}</span><span className="chip">{account?.legalCountry}</span></div>
        </section>
        <section className="panel grid" data-testid="phone-panel">
          <h3>{t("account.phone")}</h3><p className="muted">{t("account.phoneText")}</p>
          {account?.phoneVerified ? <p className="chip" data-testid="phone-done">✓ {t("account.phoneDone")}</p> : (<>
            <div className="row wrap"><input placeholder={t("account.phonePlaceholder")} value={phone} onChange={(e) => setPhone(e.target.value)} data-testid="phone" /><button className="btn" disabled={busy || !phone} data-testid="send-otp" onClick={() => run(async () => { await api.sendOtp(phone.trim()); setSent(true); notify("ok", t("account.otpSent")); })}>{t("account.sendOtp")}</button></div>
            {sent && <div className="row wrap"><input placeholder={t("account.otp")} inputMode="numeric" maxLength={6} value={code} onChange={(e) => setCode(e.target.value)} data-testid="otp" /><button className="btn primary" disabled={busy || code.length !== 6} data-testid="verify-otp" onClick={() => run(async () => { await api.verifyOtp(code); await refresh(); })}>{t("account.verify")}</button></div>}
          </>)}
        </section>
      </div>

      {account?.isMinor && (
        <section className="panel grid" data-testid="guardian-panel">
          <h3>{t("account.guardianTitle")}</h3>
          {account.guardianConsent ? <p className="chip" data-testid="guardian-done">✓ {t("account.guardianDone")}</p> : (<>
            <p className="muted">{t("account.guardianText")}</p>
            <div className="row wrap"><input type="email" placeholder={t("account.guardianEmail")} value={guardian} onChange={(e) => setGuardian(e.target.value)} data-testid="guardian-email" />
              <button className="btn" disabled={busy || !guardian} data-testid="guardian-send" onClick={() => run(async () => { await api.requestGuardian(guardian.trim()); setGuardianSent(true); notify("ok", t("account.guardianSent")); })}>{t("account.guardianSend")}</button></div>
            {guardianSent && <p className="muted">{t("account.guardianSent")}</p>}
          </>)}
        </section>
      )}

      <section className="panel grid" data-testid="fair-panel">
        <h3>{t("fair.title")}</h3><p className="muted">{t("fair.text")}</p>
        {seed && (<div className="grid">
          <div><small className="muted">{t("fair.hash")}</small><div className="mono" data-testid="seed-hash">{seed.serverSeedHash}</div></div>
          <div className="row wrap"><label>{t("fair.client")}<input value={client} onChange={(e) => setClient(e.target.value)} data-testid="client-seed" /></label>
            <button className="btn" disabled={busy || !client} data-testid="save-client" style={{ alignSelf: "end" }} onClick={() => run(async () => { setSeed(await api.setClientSeed(client)); })}>{t("fair.saveClient")}</button>
            <span className="muted" style={{ alignSelf: "end" }}>{t("fair.nonce")}: <b data-testid="nonce">{seed.nextNonce}</b></span></div>
          <div className="row"><button className="btn primary" disabled={busy} data-testid="rotate" onClick={() => run(async () => { const r = await api.rotate(); setRevealed(r.previous); setSeed(r.current); setClient(r.current.clientSeed); setHistory(await api.fairnessHistory()); setTool({ serverSeed: r.previous.serverSeed, clientSeed: r.previous.clientSeed, nonce: 1, slots: 5 }); })}>{t("fair.rotate")}</button></div>
        </div>)}
        {revealed && <div className="note" data-testid="revealed"><b>{t("fair.revealed")}</b><div className="mono">{revealed.serverSeed}</div><div className="muted">{t("fair.client")}: {revealed.clientSeed} · nonce 1…{revealed.noncesUsed}</div></div>}
        {history.length > 0 && <div><h3>{t("fair.history")}</h3><table><tbody>{history.map((h) => <tr key={h.serverSeedHash}><td className="mono">{h.serverSeedHash.slice(0, 16)}…</td><td className="mono">{h.serverSeed.slice(0, 16)}…</td><td>{h.noncesUsed}</td></tr>)}</tbody></table></div>}
        <div className="grid" data-testid="verify-tool"><h3>{t("fair.tool")}</h3>
          <div className="row wrap">
            <label>{t("fair.serverSeed")}<input value={tool.serverSeed} onChange={(e) => setTool({ ...tool, serverSeed: e.target.value })} data-testid="v-server" /></label>
            <label>{t("fair.client")}<input value={tool.clientSeed} onChange={(e) => setTool({ ...tool, clientSeed: e.target.value })} data-testid="v-client" /></label>
            <label>Nonce<input type="number" min={1} value={tool.nonce} style={{ width: 90 }} onChange={(e) => setTool({ ...tool, nonce: Number(e.target.value) })} data-testid="v-nonce" /></label>
            <label>{t("fair.slots")}<input type="number" min={1} max={20} value={tool.slots} style={{ width: 90 }} onChange={(e) => setTool({ ...tool, slots: Number(e.target.value) })} /></label>
            <button className="btn" disabled={busy || !tool.serverSeed || !tool.clientSeed} data-testid="v-run" style={{ alignSelf: "end" }} onClick={() => run(async () => { setVerify(await api.verify(tool)); })}>{t("fair.run")}</button>
          </div>
          {verify && <div className="note" data-testid="v-result"><div className="mono">{t("fair.hash")}: {verify.serverSeedHash}{history.some((h) => h.serverSeedHash === verify.serverSeedHash) ? ` — ✓ ${t("fair.hashMatches")}` : ""}</div><div>{t("fair.rolls")}: <span className="mono" data-testid="v-rolls">{verify.rolls.join(", ")}</span></div><div>{t("fair.cardRolls")}: <span className="mono">{verify.cardRolls.join(", ")}</span></div></div>}
        </div>
      </section>
    </>
  );
}
