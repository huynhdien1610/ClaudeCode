"use client";
import { useCallback, useEffect, useState } from "react";
import { api, RARITIES, type Listing } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { CardView, GLYPH } from "@/components/CardView";

const ELEMENTS = ["Luminara", "Umbryx", "Pyraxis", "Aqualis", "Terrakin", "Ventara", "Voltaris", "Nihilum"];
const PAGE = 24;

export default function MarketPage() {
  const { t, n, locale, fail, notify, setBalances, refresh } = useApp(); const { busy, run } = useAction();
  const [tab, setTab] = useState<"browse" | "mine">("browse");
  const [f, setF] = useState({ element: "", rarity: "", currency: "", q: "", sort: "newest" });
  const [items, setItems] = useState<Listing[]>([]); const [total, setTotal] = useState(0);
  const [mine, setMine] = useState<Listing[]>([]);

  const load = useCallback(async (offset = 0) => {
    try {
      const r = await api.marketListings({ ...f, limit: PAGE, offset }, locale);
      setTotal(r.total); setItems((cur) => (offset === 0 ? r.items : [...cur, ...r.items]));
    } catch (e) { fail(e); }
  }, [f, locale, fail]);
  const loadMine = useCallback(() => api.marketMine(locale).then((r) => setMine(r.items)).catch(fail), [locale, fail]);
  useEffect(() => { const h = setTimeout(() => void load(0), f.q ? 250 : 0); return () => clearTimeout(h); }, [load, f.q]);
  useEffect(() => { if (tab === "mine") void loadMine(); }, [tab, loadMine]);

  const cur = (c: string) => (c === "GEM" ? t("common.gem") : t("common.coin"));
  const buy = (l: Listing) => run(async () => { const r = await api.marketBuy(l.id); setBalances(r.balances); notify("ok", t("market.bought")); await load(0); await refresh(); });
  const cancel = (l: Listing) => run(async () => { await api.marketCancel(l.id); notify("ok", t("market.cancelled")); await loadMine(); });
  const when = (iso: string) => new Date(iso).toLocaleString(locale, { dateStyle: "short", timeStyle: "short" });

  return (
    <>
      <PageTitle sub={t("market.sub")}>{t("market.title")}</PageTitle>
      <div className="row">
        <button className={`btn ${tab === "browse" ? "primary" : ""}`} data-testid="tab-browse" onClick={() => setTab("browse")}>{t("market.browse")}</button>
        <button className={`btn ${tab === "mine" ? "primary" : ""}`} data-testid="tab-mine" onClick={() => setTab("mine")}>{t("market.mine")}</button>
      </div>

      {tab === "browse" && (<>
        <div className="filters">
          <label>{t("collection.element")}<select value={f.element} onChange={(e) => setF({ ...f, element: e.target.value })}><option value="">{t("common.all")}</option>{ELEMENTS.map((e) => <option key={e} value={e}>{GLYPH[e]} {e}</option>)}</select></label>
          <label>{t("collection.rarity")}<select value={f.rarity} onChange={(e) => setF({ ...f, rarity: e.target.value })}><option value="">{t("common.all")}</option>{RARITIES.map((r) => <option key={r} value={r}>{t(`rarity.${r}` as never)}</option>)}</select></label>
          <label>{t("market.currency")}<select value={f.currency} onChange={(e) => setF({ ...f, currency: e.target.value })}><option value="">{t("common.all")}</option><option value="COIN">{t("common.coin")}</option><option value="GEM">{t("common.gem")}</option></select></label>
          <label>{t("market.sort")}<select value={f.sort} onChange={(e) => setF({ ...f, sort: e.target.value })} data-testid="m-sort"><option value="newest">{t("market.sortNewest")}</option><option value="price_asc">{t("market.sortPriceAsc")}</option><option value="price_desc">{t("market.sortPriceDesc")}</option></select></label>
          <label>{t("common.search")}<input value={f.q} onChange={(e) => setF({ ...f, q: e.target.value })} /></label>
        </div>
        {items.length === 0 && <div className="panel"><p>{t("market.empty")}</p></div>}
        <div className="card-grid" data-testid="market-grid">
          {items.map((l) => (
            <div key={l.id} className="card-wrap" data-testid="listing" data-price={l.price} data-currency={l.currency} data-mine={l.mine}>
              <CardView card={l.card} edition={l.edition} />
              <b className={`cur ${l.currency === "GEM" ? "gem" : "coin"}`} data-testid="listing-price">{n(l.price)} {cur(l.currency)}</b>
              <small className="muted">{t("market.expires", { d: when(l.expiresAt) })}</small>
              {l.mine ? <span className="chip">{t("market.yours")}</span> : <button className="btn primary sm" disabled={busy} data-testid="buy-listing" onClick={() => void buy(l)}>{t("market.buy")}</button>}
            </div>
          ))}
        </div>
        {items.length < total && <button className="btn" onClick={() => void load(items.length)}>{t("market.more")}</button>}
      </>)}

      {tab === "mine" && (
        <div className="panel"><table data-testid="my-listings"><tbody>
          {mine.map((l) => (
            <tr key={l.id} data-testid="my-listing" data-status={l.status}>
              <td><b>{l.card.name}</b> <small className="muted">{l.edition}</small></td>
              <td>{n(l.price)} {cur(l.currency)}</td>
              <td><span className="chip">{t(`market.status.${l.status}` as never)}{l.boughtByMe ? ` · ${t("market.bought2")}` : ""}</span></td>
              <td className="muted">{when(l.createdAt)}</td>
              <td>{l.status === "Active" && l.mine && <button className="btn sm" disabled={busy} data-testid="cancel-listing" onClick={() => void cancel(l)}>{t("market.cancel")}</button>}</td>
            </tr>
          ))}
          {mine.length === 0 && <tr><td className="muted">{t("market.empty")}</td></tr>}
        </tbody></table></div>
      )}
    </>
  );
}
