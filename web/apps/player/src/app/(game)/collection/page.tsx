"use client";
import { useCallback, useEffect, useState } from "react";
import { api, RARITIES, type Card, type Collection, type MarketRules, type OwnedCard, type Progress } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle, Modal } from "@/components/Shell";
import { CardView, GLYPH } from "@/components/CardView";

const ELEMENTS = ["Luminara", "Umbryx", "Pyraxis", "Aqualis", "Terrakin", "Ventara", "Voltaris", "Nihilum"];
const TYPES = ["anima", "echo", "seal"];

export default function CollectionPage() {
  const { t, n, locale, fail, notify } = useApp(); const { busy, run } = useAction();
  const [rules, setRules] = useState<MarketRules | null>(null);
  const [sell, setSell] = useState({ currency: "COIN", price: "", days: 3 });
  const [q, setQ] = useState({ element: "", rarity: "", type: "", q: "" });
  const [data, setData] = useState<Collection | null>(null);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [detail, setDetail] = useState<{ owned: OwnedCard; card: Card } | null>(null);

  const load = useCallback(() => api.collection(q).then(setData).catch(fail), [q, fail]);
  useEffect(() => { const h = setTimeout(load, q.q ? 250 : 0); return () => clearTimeout(h); }, [load, q.q]);
  useEffect(() => { api.progress().then(setProgress).catch(fail); }, [fail]);

  useEffect(() => { api.marketRules().then(setRules).catch(() => undefined); }, []);
  const open = async (o: OwnedCard) => { try { setDetail({ owned: o, card: await api.card(o.card.id, locale) }); } catch (e) { fail(e); } };

  return (
    <>
      <PageTitle sub={progress && <span data-testid="progress-text">{t("collection.progress", { owned: progress.owned, total: progress.total })}</span>}>{t("collection.title")}</PageTitle>
      {progress && <div className="progress"><i style={{ width: `${progress.percent}%` }} /></div>}
      <div className="filters">
        <label>{t("collection.element")}<select value={q.element} onChange={(e) => setQ({ ...q, element: e.target.value })} data-testid="f-element"><option value="">{t("common.all")}</option>{ELEMENTS.map((e) => <option key={e} value={e}>{GLYPH[e]} {e}</option>)}</select></label>
        <label>{t("collection.rarity")}<select value={q.rarity} onChange={(e) => setQ({ ...q, rarity: e.target.value })} data-testid="f-rarity"><option value="">{t("common.all")}</option>{RARITIES.map((r) => <option key={r} value={r}>{t(`rarity.${r}` as never)}</option>)}</select></label>
        <label>{t("collection.type")}<select value={q.type} onChange={(e) => setQ({ ...q, type: e.target.value })} data-testid="f-type"><option value="">{t("common.all")}</option>{TYPES.map((x) => <option key={x} value={x}>{t(`type.${x}` as never)}</option>)}</select></label>
        <label>{t("common.search")}<input value={q.q} onChange={(e) => setQ({ ...q, q: e.target.value })} data-testid="f-q" /></label>
      </div>
      {data && data.items.length === 0 && <div className="panel"><p>{t("common.empty")}</p></div>}
      <div className="card-grid" data-testid="collection-grid">
        {data?.items.map((o) => <div key={o.id} className="card-wrap" data-testid="owned-card"><CardView card={o.card} edition={o.edition} soulbound={o.soulbound} onClick={() => open(o)} /></div>)}
      </div>
      {detail && (
        <Modal label={detail.card.name} onClose={() => setDetail(null)}>
          <div className="row wrap" style={{ alignItems: "flex-start" }} data-testid="card-detail">
            <CardView card={detail.card} rarity={detail.owned.card.rarity} edition={detail.owned.edition} soulbound={detail.owned.soulbound} size={200} />
            <div style={{ flex: 1, minWidth: 240 }}>
              <h2>{detail.card.name}</h2>
              {detail.card.epithet && <p className="muted">{detail.card.epithet}</p>}
              <div className="row wrap"><span className="chip">{GLYPH[detail.card.element]} {detail.card.element} · {t(`el.${detail.card.element}` as never)}</span><span className={`chip rarity-${detail.card.rarity}`}><b className="rarity-text">{t(`rarity.${detail.card.rarity}` as never)}</b></span>{detail.owned.soulbound && <span className="chip">{t("common.soulbound")}</span>}</div>
              {detail.card.type === "anima" && <p style={{ marginTop: 8 }}>{t("collection.stats", { c: detail.card.cost, a: detail.card.atk ?? 0, d: detail.card.def ?? 0, h: detail.card.hp ?? 0 })}</p>}
              <p className="muted">{t("collection.edition", { e: detail.owned.edition })} · {t("collection.serial", { n: detail.owned.serial })}</p>
              <p className="muted">{t("collection.supply", { issued: detail.card.supply.issued, max: detail.card.supply.max, burned: detail.card.supply.burned })}</p>
              <h3>{t("market.sell")}</h3>
              {detail.owned.soulbound ? <p className="note">{t("market.notTradable")}</p> : detail.owned.state !== "Owned" ? <p className="note">{t("market.locked")}</p> : (() => {
                const range = rules?.ranges.find((r) => r.rarity === detail.card.rarity);
                const rg = sell.currency === "GEM" ? range?.gem : range?.coin;
                const price = Number(sell.price); const fee = rules ? Math.ceil((price * rules.feePercent) / 100) : 0;
                return (
                  <div className="grid" data-testid="sell-form">
                    <div className="row wrap">
                      <label>{t("market.currency")}<select value={sell.currency} onChange={(e) => setSell({ ...sell, currency: e.target.value })} data-testid="sell-currency"><option value="COIN">{t("common.coin")}</option><option value="GEM">{t("common.gem")}</option></select></label>
                      <label>{t("market.price")}<input type="number" min={1} value={sell.price} onChange={(e) => setSell({ ...sell, price: e.target.value })} data-testid="sell-price" style={{ width: 120 }} /></label>
                      <label>{t("market.duration")}<select value={sell.days} onChange={(e) => setSell({ ...sell, days: Number(e.target.value) })}>{(rules?.durations ?? [1, 3, 7]).map((d) => <option key={d} value={d}>{t("market.days", { n: d })}</option>)}</select></label>
                    </div>
                    {rg && <small className="muted">{t("market.range", { min: n(rg.min), max: n(rg.max), cur: sell.currency === "GEM" ? t("common.gem") : t("common.coin") })}</small>}
                    {price > 0 && rules && <small className="muted">{t("market.receive", { n: n(price - fee), cur: sell.currency === "GEM" ? t("common.gem") : t("common.coin"), p: rules.feePercent })}</small>}
                    <button className="btn primary" disabled={busy || !(price > 0)} data-testid="sell-go" onClick={() => void run(async () => { await api.marketList(detail.owned.id, sell.currency, price, sell.days); notify("ok", t("market.listed")); setDetail(null); await load(); })}>{t("market.listBtn")}</button>
                  </div>
                );
              })()}
              <h3>{t("collection.story")}</h3>
              {detail.card.story ? <p style={{ fontStyle: "italic", color: "var(--text)" }} data-testid="story">“{detail.card.story}”</p> : <p className="note">{t("collection.storyLocked")}</p>}
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}
