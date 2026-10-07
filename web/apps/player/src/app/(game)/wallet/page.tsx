"use client";
import { useCallback, useEffect, useState } from "react";
import { api, type GemPackage, type LedgerEntry } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";

export default function WalletPage() {
  const { t, n, balances, setBalances, notify, fail, locale } = useApp(); const { busy, run } = useAction();
  const [pkgs, setPkgs] = useState<GemPackage[]>([]);
  const [eco, setEco] = useState<Record<string, number> | null>(null);
  const [ledger, setLedger] = useState<LedgerEntry[]>([]);
  const [dir, setDir] = useState<"GEM_TO_COIN" | "COIN_TO_GEM">("GEM_TO_COIN");
  const [amount, setAmount] = useState(10);
  const load = useCallback(() => { api.ledger().then(setLedger).catch(fail); }, [fail]);
  useEffect(() => { api.economy().then(setEco).catch(fail); api.gemPackages().then(setPkgs).catch(fail); load(); }, [load, fail]);

  const convert = () => run(async () => {
    const r = await api.convert(dir, amount);
    setBalances(r.balances); notify("ok", t("wallet.converted", { s: n(r.spent), r: n(r.received) })); load();
  });
  const price = (p: GemPackage) => new Intl.NumberFormat(locale, { style: "currency", currency: p.currency, maximumFractionDigits: 0 }).format(p.priceMinor);
  const buy = (code: string) => run(async () => {
    const r = await api.createOrder(code);
    const u = new URL(r.checkoutUrl, window.location.origin);
    window.location.assign(u.origin === window.location.origin ? u.pathname + u.search : r.checkoutUrl);   // cổng thật nằm ở miền khác
  });
  const devTopUp = process.env.NEXT_PUBLIC_DEV_TOPUP === "1";

  return (
    <>
      <PageTitle>{t("wallet.title")}</PageTitle>
      <div className="cols">
        <section className="panel"><h3>{t("common.coin")}</h3><p className="cur coin" style={{ fontSize: 30, margin: 0 }} data-testid="coin">{n(balances?.coin ?? 0)}</p></section>
        <section className="panel"><h3>{t("common.gem")}</h3><p className="cur gem" style={{ fontSize: 30, margin: 0 }} data-testid="gem">{n(balances?.gem ?? 0)}</p></section>
      </div>
      <div className="cols">
        <section className="panel grid">
          <h3>{t("wallet.convert")}</h3>
          {eco && <p className="muted">{t("wallet.rate", { a: eco.gem_to_coin_rate, b: eco.coin_to_gem_rate })}</p>}
          <div className="row wrap">
            <label>{t("wallet.convert")}<select value={dir} onChange={(e) => setDir(e.target.value as typeof dir)} data-testid="conv-dir"><option value="GEM_TO_COIN">{t("wallet.gemToCoin")}</option><option value="COIN_TO_GEM">{t("wallet.coinToGem")}</option></select></label>
            <label>{t("wallet.amount")}<input type="number" min={1} value={amount} onChange={(e) => setAmount(Number(e.target.value))} data-testid="conv-amount" style={{ width: 120 }} /></label>
            <button className="btn primary" disabled={busy || amount < 1} onClick={convert} data-testid="conv-go" style={{ alignSelf: "end" }}>{t("wallet.convertGo")}</button>
          </div>
          <p className="note">{t("wallet.coinGemNote")}</p>
        </section>
        {pkgs.length > 0 && (
          <section className="panel grid" data-testid="gem-packages"><h3>{t("wallet.buyGem")}</h3><p className="muted">{t("wallet.buyGemNote")}</p>
            <div className="row wrap">{pkgs.map((p) => <button key={p.code} className="btn" disabled={busy} data-testid={`buy-${p.code}`} onClick={() => void buy(p.code)}><b className="cur gem">{n(p.gem)}</b>&nbsp;{t("common.gem")} · {price(p)}</button>)}</div>
          </section>
        )}
        {devTopUp && (
          <section className="panel grid"><h3>{t("wallet.topup")}</h3><p className="muted">{t("wallet.topupNote")}</p>
            <div className="row wrap">{[100, 1000].map((g) => <button key={g} className="btn" disabled={busy} data-testid={`topup-${g}`} onClick={() => run(async () => { setBalances(await api.devTopUp(g)); load(); })}>{t("wallet.topupGo", { n: g })}</button>)}</div>
          </section>
        )}
      </div>
      <section className="panel"><h3>{t("wallet.ledger")}</h3>
        <table data-testid="ledger"><tbody>{ledger.map((e) => (
          <tr key={e.id}><td className="muted">{new Date(e.createdAt).toLocaleString(locale)}</td><td>{t(`reason.${e.reason}` as never) === `reason.${e.reason}` ? e.reason : t(`reason.${e.reason}` as never)}</td>
            <td style={{ textAlign: "right", color: e.amount > 0 ? "var(--ok)" : "var(--danger)", fontWeight: 700 }}>{e.amount > 0 ? "+" : ""}{n(e.amount)} {e.currency === "GEM" ? t("common.gem") : t("common.coin")}</td></tr>
        ))}</tbody></table>
      </section>
    </>
  );
}
