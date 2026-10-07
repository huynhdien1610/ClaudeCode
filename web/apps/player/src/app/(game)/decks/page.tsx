"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { api, type DeckView, type OwnedCard } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { canAdd, deckStats, MAX_DECKS, validateDeck, type DeckEntry } from "@/lib/deck";

const entry = (o: OwnedCard): DeckEntry => ({ defId: o.card.id, type: o.card.type, rarity: o.card.rarity });

export default function DecksPage() {
  const { t, fail, notify } = useApp(); const { busy, run } = useAction();
  const [decks, setDecks] = useState<DeckView[] | null>(null);
  const [owned, setOwned] = useState<OwnedCard[]>([]);
  const [sel, setSel] = useState<string | "new" | null>(null);
  const [name, setName] = useState("");
  const [picked, setPicked] = useState<string[]>([]);

  const load = useCallback(async () => {
    try { const [d, c] = await Promise.all([api.decks(), api.collection({})]); setDecks(d); setOwned(c.items.filter((i) => i.state === "Owned")); } catch (e) { fail(e); }
  }, [fail]);
  useEffect(() => { void load(); }, [load]);

  const byId = useMemo(() => new Map(owned.map((o) => [o.id, o])), [owned]);
  const open = (d: DeckView | "new") => { setSel(d === "new" ? "new" : d.id); setName(d === "new" ? "" : d.name); setPicked(d === "new" ? [] : d.cardInstanceIds); };
  const current = decks?.find((d) => d.id === sel) ?? null;

  const pickedEntries = picked.map((id) => byId.get(id)).filter((o): o is OwnedCard => !!o).map(entry);
  const stats = deckStats(pickedEntries);
  const rules = validateDeck(pickedEntries);
  const toggle = (o: OwnedCard) => setPicked((p) => (p.includes(o.id) ? p.filter((x) => x !== o.id) : [...p, o.id]));
  const sorted = (ids: string[]) => ids.map((id) => byId.get(id)).filter((o): o is OwnedCard => !!o).sort((a, b) => a.card.name.localeCompare(b.card.name));

  const save = () => run(async () => {
    const d = sel === "new" ? await api.createDeck(name, picked) : await api.saveDeck(sel!, name, picked);
    notify("ok", t("decks.saved")); await load(); open(d);
  });

  return (
    <>
      <PageTitle sub={t("decks.sub")}>{t("decks.title")}</PageTitle>
      <div className="cols">
        <section className="panel grid" data-testid="deck-list">
          {decks?.length === 0 && <p className="muted">{t("decks.empty")}</p>}
          {decks?.map((d) => (
            <button key={d.id} className={`btn ${sel === d.id ? "primary" : ""}`} data-testid="deck-item" onClick={() => open(d)} style={{ justifyContent: "space-between" }}>
              <span>{d.name}</span>
              <span className="row">{d.isDefault && <span className="chip">{t("decks.isDefault")}</span>}<span className={`chip ${d.valid ? "" : "muted"}`}>{d.valid ? t("decks.valid") : t("decks.count", { n: d.count })}</span></span>
            </button>
          ))}
          <button className="btn" data-testid="deck-new" disabled={busy || (decks?.length ?? 0) >= MAX_DECKS} onClick={() => open("new")}>+ {t("decks.new")}</button>
        </section>

        {sel && (
          <section className="panel grid" data-testid="deck-editor">
            <label>{t("decks.name")}<input value={name} maxLength={40} onChange={(e) => setName(e.target.value)} data-testid="deck-name" /></label>
            <div className="row wrap">
              <span className={`chip ${stats.total === 30 ? "" : "muted"}`} data-testid="deck-count">{t("decks.count", { n: stats.total })}</span>
              <span className="chip">{t("decks.anima", { a: stats.anima })}</span><span className="chip">{t("decks.support", { s: stats.support })}</span>
              <span className="chip" data-testid="deck-valid" data-valid={rules.length === 0}>{rules.length === 0 ? t("decks.valid") : t("decks.invalid")}</span>
            </div>
            {rules.length > 0 && <ul className="muted" style={{ margin: 0, paddingLeft: 18 }}>{rules.map((r) => <li key={r}>{t(`decks.rule.${r}` as never)}</li>)}</ul>}
            <div className="row wrap">
              <button className="btn primary" disabled={busy || !name.trim()} data-testid="deck-save" onClick={() => void save()}>{t("decks.save")}</button>
              {current && !current.isDefault && <button className="btn" disabled={busy} onClick={() => void run(async () => { await api.defaultDeck(current.id); await load(); })}>{t("decks.setDefault")}</button>}
              {current && <button className="btn ghost" disabled={busy} onClick={() => void run(async () => { await api.deleteDeck(current.id); setSel(null); await load(); })}>{t("decks.delete")}</button>}
            </div>
            <h3>{t("decks.inDeck")}</h3>
            <ul className="deck-rows" data-testid="deck-cards">{sorted(picked).map((o) => <li key={o.id}><button className="btn sm ghost" onClick={() => toggle(o)}>− {o.card.name} <small className="muted">({t(`rarity.${o.card.rarity}` as const)})</small></button></li>)}</ul>
          </section>
        )}
      </div>

      {sel && (
        <section className="panel" data-testid="deck-available">
          <h3>{t("decks.available")}</h3>
          <ul className="deck-rows">
            {owned.filter((o) => !picked.includes(o.id)).sort((a, b) => a.card.name.localeCompare(b.card.name)).map((o) => (
              <li key={o.id}><button className="btn sm" data-testid="deck-add" disabled={!canAdd(pickedEntries, entry(o))} onClick={() => toggle(o)}>+ {o.card.name} <small className="muted">({t(`rarity.${o.card.rarity}` as const)} · {t(`type.${o.card.type}` as const)} · {o.edition})</small></button></li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}
