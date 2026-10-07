"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { api, newKey, type PackView } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { OddsTable } from "@/components/OddsTable";

export default function StorePage() {
  const { t, n, setBalances, notify, fail, balances } = useApp(); const { busy, run } = useAction();
  const [packs, setPacks] = useState<PackView[] | null>(null);
  const [pity, setPity] = useState<{ count: number; guaranteeAfter: number } | null>(null);
  const [qty, setQty] = useState(1);
  useEffect(() => { api.packs().then(setPacks).catch(fail); api.pity("awakening-standard").then(setPity).catch(fail); }, [fail]);

  const buy = (code: string, currency: "GEM" | "COIN") => run(async () => {
    const r = await api.buy(code, currency, qty, newKey());
    setBalances(r.balances); notify("ok", t("store.bought"));
    api.pity(code).then(setPity).catch(() => undefined);
  });

  return (
    <>
      <PageTitle>{t("store.title")}</PageTitle>
      {packs?.map(({ pack, odds }) => (
        <section key={pack.code} className="panel grid" data-testid={`pack-${pack.code}`}>
          <div className="row wrap">
            <div className="pack-art" style={{ width: 96, cursor: "default" }} aria-hidden>ANIMA</div>
            <div style={{ flex: 1, minWidth: 240 }}>
              <h2>{pack.name}</h2><p>{t("store.perPack", { n: pack.cardsPerPack })}</p>
              {pity && <p className="muted">{t("store.pity", { n: pity.count, max: pity.guaranteeAfter })}</p>}
              <div className="row wrap">
                <label style={{ width: 90 }}>{t("store.quantity")}<input type="number" min={1} max={10} value={qty} onChange={(e) => setQty(Math.max(1, Math.min(10, Number(e.target.value) || 1)))} data-testid="qty" /></label>
                {pack.priceCoin != null && <button className="btn primary" disabled={busy || !pack.onSale || (balances?.coin ?? 0) < pack.priceCoin * qty} onClick={() => buy(pack.code, "COIN")} data-testid="buy-coin">{t("store.payCoin", { n: n(pack.priceCoin * qty) })}</button>}
                {pack.priceGem != null && <button className="btn primary" disabled={busy || !pack.onSale || (balances?.gem ?? 0) < pack.priceGem * qty} onClick={() => buy(pack.code, "GEM")} data-testid="buy-gem">{t("store.payGem", { n: n(pack.priceGem * qty) })}</button>}
                <Link className="btn" href="/packs">{t("nav.packs")}</Link>
              </div>
              {!pack.onSale && <p className="note">{t("store.offSale")}</p>}
            </div>
          </div>
          {odds && (<div><h3>{t("store.odds")}</h3><OddsTable entries={odds.entries} /><p className="muted" style={{ marginTop: 8 }}>{t("store.oddsNote")}</p></div>)}
        </section>
      ))}
    </>
  );
}
