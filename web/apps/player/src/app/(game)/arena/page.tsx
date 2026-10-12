"use client";
import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { api, type ArenaHistoryItem, type ArenaOverview, type ArenaRank, type ArenaView, type DeckView } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { BattleBoard } from "@/components/BattleBoard";

const POLL_MS = 1500;

export default function ArenaPage() {
  const { t, n, fail } = useApp(); const { busy, run } = useAction();
  const [o, setO] = useState<ArenaOverview | null>(null);
  const [decks, setDecks] = useState<DeckView[]>([]); const [deckId, setDeckId] = useState("");
  const [hist, setHist] = useState<ArenaHistoryItem[]>([]); const [board, setBoard] = useState<ArenaRank[]>([]);
  const [view, setView] = useState<ArenaView | null>(null);
  const [tick, setTick] = useState(0); const skew = useRef(0);

  const adopt = useCallback((v: ArenaView) => { skew.current = Date.parse(v.clock.serverTime) - Date.now(); setView(v); }, []);

  const loadLobby = useCallback(async () => {
    try {
      const [ov, d, h, b] = await Promise.all([api.arena(), api.decks(), api.arenaHistory(), api.arenaLeaderboard()]);
      setO(ov); const valid = d.filter((x) => x.valid); setDecks(valid); setHist(h); setBoard(b);
      setDeckId((cur) => cur || valid.find((x) => x.isDefault)?.id || valid[0]?.id || "");
      if (ov.activeMatchId) adopt(await api.arenaMatch(ov.activeMatchId));
    } catch (e) { fail(e); }
  }, [fail, adopt]);
  useEffect(() => { void loadLobby(); }, [loadLobby]);

  // Đang chờ ghép: hỏi lại tổng quan để máy chủ thử ghép (cửa sổ điểm rộng dần theo thời gian chờ).
  const searching = !view && o?.queue != null;
  useEffect(() => {
    if (!searching) return;
    const h = setInterval(() => { api.arena().then((ov) => { setO(ov); if (ov.activeMatchId) void api.arenaMatch(ov.activeMatchId).then(adopt); }).catch(fail); }, 2000);
    return () => clearInterval(h);
  }, [searching, fail, adopt]);

  // Đang đánh: lấy trạng thái đối thủ định kỳ; đồng hồ đếm ngược chạy mỗi giây.
  const live = view?.status === "InProgress" ? view.id : null;
  useEffect(() => {
    if (!live) return;
    const poll = setInterval(() => { api.arenaMatch(live).then(adopt).catch(fail); }, POLL_MS);
    const clock = setInterval(() => setTick((x) => x + 1), 1000);
    return () => { clearInterval(poll); clearInterval(clock); };
  }, [live, fail, adopt]);

  if (view) {
    const left = view.clock.deadline ? Math.max(0, Math.ceil((Date.parse(view.clock.deadline) - (Date.now() + skew.current)) / 1000)) : null;
    void tick;
    const header = (
      <section className="panel row wrap" data-testid="arena-header">
        <span className="chip">{t("arena.vs", { name: view.opponentName })}</span>
        {left !== null && <span className="chip" data-testid="arena-clock">{t("arena.clock", { s: left })}</span>}
        {view.clock.bankMs && <span className="chip muted">{t("arena.bank", { s: Math.round(view.clock.bankMs.you / 1000) })}</span>}
        {view.rating && <span className="chip" data-testid="arena-rating">{t("arena.ratingDelta", { before: view.rating.before, delta: view.rating.delta === null ? "" : `${view.rating.delta > 0 ? "+" : ""}${view.rating.delta}`, ap: view.rating.ap })}</span>}
      </section>
    );
    return <BattleBoard view={view} header={header} onAct={async (a) => { adopt(await api.arenaAct(view.id, a)); }} onBack={() => { setView(null); void loadLobby(); }} />;
  }

  const join = () => run(async () => { const r = await api.arenaJoin(deckId); if (r.matchId) adopt(await api.arenaMatch(r.matchId)); else setO(await api.arena()); });
  const leave = () => run(async () => { await api.arenaLeave(); setO(await api.arena()); });

  return (
    <>
      <PageTitle sub={t("arena.sub")}>{t("arena.title")}</PageTitle>
      {o && (
        <section className="panel row wrap" data-testid="arena-stats">
          <span className="chip" data-testid="arena-rating-now">{t("arena.rating", { n: o.rating, rd: o.rd })}</span>
          <span className="chip">{t("arena.record", { w: o.wins, g: o.games })}</span>
          <span className="chip" data-testid="arena-ap">{t("arena.ap", { n: o.ap })}</span>
        </section>
      )}
      <section className="panel grid">
        {decks.length === 0 ? <><p>{t("battle.noDeck")}</p><Link className="btn" href="/decks">{t("battle.openDecks")}</Link></> : o?.queue ? (
          <div className="row wrap"><span data-testid="arena-searching">{t("arena.searching", { s: o.queue.waitedSeconds })}</span><button className="btn" disabled={busy} data-testid="arena-leave" onClick={() => void leave()}>{t("arena.leave")}</button></div>
        ) : (
          <div className="row wrap">
            <label>{t("battle.pickDeck")}<select value={deckId} onChange={(e) => setDeckId(e.target.value)} data-testid="arena-deck">{decks.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
            <button className="btn primary" disabled={busy || !deckId} data-testid="arena-join" style={{ alignSelf: "end" }} onClick={() => void join()}>{t("arena.join")}</button>
          </div>
        )}
        <p className="muted">{t("arena.rules", { turn: o?.rules.turnSeconds ?? 20, bank: o?.rules.bankSeconds ?? 30, ap: o?.rules.winAp ?? 10 })}</p>
      </section>
      {hist.length > 0 && (
        <section className="panel"><h3>{t("battle.history")}</h3>
          <table data-testid="arena-history"><tbody>{hist.map((m) => (
            <tr key={m.id}><td className="muted">{new Date(m.createdAt).toLocaleString()}</td><td>{m.opponentName}</td>
              <td>{m.status === "InProgress" ? t("battle.resultOpen") : m.won ? t("battle.resultWon") : t("battle.resultLost")}</td>
              <td className="muted">{m.ratingDelta !== null ? `${m.ratingDelta > 0 ? "+" : ""}${m.ratingDelta}` : ""}{m.ap > 0 ? ` · +${m.ap} AP` : ""}</td></tr>))}</tbody></table>
        </section>
      )}
      {board.length > 0 && (
        <section className="panel"><h3>{t("arena.leaderboard")}</h3>
          <table data-testid="arena-board"><tbody>{board.map((r, i) => <tr key={r.name}><td>{i + 1}</td><td>{r.name}</td><td>{n(r.rating)}</td><td className="muted">{t("arena.record", { w: r.wins, g: r.games })}</td></tr>)}</tbody></table>
        </section>
      )}
    </>
  );
}
