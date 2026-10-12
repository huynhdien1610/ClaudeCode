"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type BattleSummary, type BattleView, type DeckView } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { BattleBoard } from "@/components/BattleBoard";

export default function BattlePage() {
  const { t, fail } = useApp(); const { busy, run } = useAction();
  const [decks, setDecks] = useState<DeckView[]>([]);
  const [deckId, setDeckId] = useState("");
  const [history, setHistory] = useState<BattleSummary[]>([]);
  const [view, setView] = useState<BattleView | null>(null);

  const loadMenu = useCallback(async () => {
    try {
      const [d, h] = await Promise.all([api.decks(), api.battles()]);
      const valid = d.filter((x) => x.valid); setDecks(valid); setHistory(h);
      setDeckId((cur) => cur || valid.find((x) => x.isDefault)?.id || valid[0]?.id || "");
      const active = h.find((m) => m.status === "InProgress");
      if (active) setView(await api.battle(active.id));
    } catch (e) { fail(e); }
  }, [fail]);
  useEffect(() => { void loadMenu(); }, [loadMenu]);

  // ---------- Menu ----------
  if (!view) {
    return (
      <>
        <PageTitle sub={t("battle.sub")}>{t("battle.title")}</PageTitle>
        <section className="panel grid">
          {decks.length === 0 ? <><p>{t("battle.noDeck")}</p><Link className="btn" href="/decks">{t("battle.openDecks")}</Link></> : (
            <div className="row wrap">
              <label>{t("battle.pickDeck")}<select value={deckId} onChange={(e) => setDeckId(e.target.value)} data-testid="battle-deck">{decks.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
              <button className="btn primary" disabled={busy || !deckId} data-testid="battle-start" style={{ alignSelf: "end" }} onClick={() => void run(async () => { setView(await api.startPractice(deckId)); })}>{t("battle.start")}</button>
            </div>
          )}
        </section>
        {history.length > 0 && (
          <section className="panel"><h3>{t("battle.history")}</h3>
            <table data-testid="battle-history"><tbody>{history.map((m) => (
              <tr key={m.id}><td className="muted">{new Date(m.createdAt).toLocaleString()}</td>
                <td>{m.status === "InProgress" ? t("battle.resultOpen") : m.won ? t("battle.resultWon") : t("battle.resultLost")}</td>
                <td className="muted">{m.reason ? t(`battle.reason.${m.reason}` as never) : ""}</td></tr>))}</tbody></table>
          </section>
        )}
      </>
    );
  }

  return <BattleBoard view={view} onAct={async (a) => { setView(await api.battleAct(view.id, a)); }} onBack={() => { setView(null); void loadMenu(); }} />;
}
