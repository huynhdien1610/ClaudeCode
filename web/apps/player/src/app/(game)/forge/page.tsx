"use client";
import { useCallback, useEffect, useState } from "react";
import { api, newKey, type ForgeInfo, type OpenedCard, type OwnedCard, type SealedCard } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { CardView } from "@/components/CardView";
import { OddsTable } from "@/components/OddsTable";
import { PackOpening } from "@/components/PackOpening";

export default function ForgePage() {
  const { t, n, balances, setBalances, notify, refresh, fail } = useApp(); const { busy, run } = useAction();
  const [info, setInfo] = useState<ForgeInfo | null>(null);
  const [cards, setCards] = useState<OwnedCard[]>([]);
  const [sealed, setSealed] = useState<SealedCard[]>([]);
  const [sel, setSel] = useState<string[]>([]);
  const [revealed, setRevealed] = useState<OpenedCard | null>(null);

  const load = useCallback(async () => {
    try {
      const [i, c, s] = await Promise.all([api.forgeInfo(), api.collection({}), api.sealed()]);
      setInfo(i); setCards(c.items.filter((x) => !x.soulbound && x.state === "Owned")); setSealed(s);
    } catch (e) { fail(e); }
  }, [fail]);
  useEffect(() => { load(); }, [load]);

  const toggle = (id: string) => setSel((s) => s.includes(id) ? s.filter((x) => x !== id) : s.length >= 2 ? [s[1], id] : [...s, id]);
  const forge = (cur: "COIN" | "GEM") => run(async () => {
    const r = await api.forge(sel, cur, newKey());
    setBalances(r.balances); setSel([]); notify("ok", t("forge.created")); await load();
  });

  return (
    <>
      <PageTitle sub={t("forge.intro")}>{t("forge.title")}</PageTitle>
      {info && <div className="row wrap"><span className="chip">{t("forge.fee", { coin: info.feeCoin, gem: info.feeGem })}</span><span className="chip" data-testid="forge-used">{t("forge.used", { used: info.usedToday, limit: info.dailyLimit })}</span></div>}

      {sealed.length > 0 && (
        <section className="panel grid" data-testid="sealed-section">
          <h3>{t("forge.sealed")}</h3>
          <div className="row wrap">{sealed.map((s) => (
            <div key={s.id} className="card-wrap"><CardView card={{ id: 0, code: "", name: "?", epithet: null, story: null, season: "", element: "Nihilum", rarity: "common", type: "anima", cost: 0, atk: null, def: null, hp: null, skill: null, arc: null, supply: { max: 0, issued: 0, burned: 0, remaining: 0 } }} flipped={false} size={110} />
              <button className="btn primary sm" disabled={busy} data-testid="reveal-sealed" onClick={() => run(async () => { const r = await api.reveal(s.id); setRevealed(r.card); })}>{t("forge.reveal")}</button></div>
          ))}</div>
        </section>
      )}

      <section className="panel grid">
        <h3>{t("forge.select", { n: sel.length })}</h3>
        {cards.length === 0 && <p className="muted">{t("forge.noCards")}</p>}
        <div className="card-grid" data-testid="forge-cards">
          {cards.map((c) => <div key={c.id} className={`card-wrap ${sel.includes(c.id) ? "sel" : ""}`} data-testid="forge-card" data-selected={sel.includes(c.id)}><CardView card={c.card} edition={c.edition} onClick={() => toggle(c.id)} size={120} /></div>)}
        </div>
        <div className="row wrap">
          <span className="muted">{t("forge.payWith")}</span>
          <button className="btn primary" disabled={busy || sel.length !== 2 || (balances?.coin ?? 0) < (info?.feeCoin ?? 0)} onClick={() => forge("COIN")} data-testid="forge-coin">{t("forge.go")} · {n(info?.feeCoin ?? 0)} {t("common.coin")}</button>
          <button className="btn primary" disabled={busy || sel.length !== 2 || (balances?.gem ?? 0) < (info?.feeGem ?? 0)} onClick={() => forge("GEM")} data-testid="forge-gem">{t("forge.go")} · {n(info?.feeGem ?? 0)} {t("common.gem")}</button>
        </div>
        <p className="muted">{t("forge.bound")}</p>
      </section>

      {info && <section className="panel"><h3>{t("forge.oddsTitle")}</h3><OddsTable entries={info.odds.entries} /></section>}
      {revealed && <PackOpening title={t("forge.title")} cards={[revealed]} onClose={() => { setRevealed(null); load(); refresh(); }} />}
    </>
  );
}
