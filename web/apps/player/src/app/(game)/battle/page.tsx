"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type BattleAnima, type BattleEvent, type BattleFusion, type BattleSkill, type BattleStatus, type BattleSummary, type BattleView, type DeckView } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";

type Target = "enemy" | "ally";
type Sel =
  | { kind: "hand"; index: number }
  | { kind: "actor"; slot: number }
  | { kind: "attack"; slot: number }
  | { kind: "skill" | "ultimate"; slot: number; name: string; target: Target }
  | { kind: "fusion"; id: string; name: string; target: Target }
  | null;

export default function BattlePage() {
  const { t, n, fail } = useApp(); const { busy, run } = useAction();
  const [decks, setDecks] = useState<DeckView[]>([]);
  const [deckId, setDeckId] = useState("");
  const [history, setHistory] = useState<BattleSummary[]>([]);
  const [view, setView] = useState<BattleView | null>(null);
  const [sel, setSel] = useState<Sel>(null);

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

  const act = (a: { type: string; hand?: number; slot?: number; target?: number; id?: string }) => run(async () => { if (!view) return; setView(await api.battleAct(view.id, a)); setSel(null); });
  const who = (side: string) => (side === "you" ? t("battle.you") : t("battle.opponent"));
  const statusLabel = (st: BattleStatus) => {
    const base = t(`status.${st.kind}` as never);
    const v = st.kind.endsWith("_pct") ? ` ${st.value > 0 ? "+" : ""}${Math.round(st.value)}%` : st.kind === "burn" ? ` ${Math.round(st.value)}×${st.stacks}` : st.stacks > 1 ? ` ×${st.stacks}` : "";
    return `${base}${v}${st.turns ? ` (${st.turns})` : ""}`;
  };
  const eventText = (e: BattleEvent) => {
    const d = e.data as Record<string, string | number>;
    const side = e.kind === "start" ? (d.first === 0 ? "you" : "opponent") : e.side;
    const params = Object.fromEntries(Object.entries(d).map(([k, v]) => [k, k === "status" ? t(`status.${String(v)}` as never) : String(v)]));
    return t(`battle.ev.${e.kind}` as never, { who: who(side), ...params } as never);
  };

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

  // ---------- Bàn đấu ----------
  const myTurn = view.yourTurn && view.status === "InProgress";
  const oppEmpty = view.opponent.field.every((f) => f === null);
  const targeting = sel && (sel.kind === "attack" || sel.kind === "skill" || sel.kind === "ultimate" || sel.kind === "fusion") ? sel : null;
  const enemyTargeting = targeting && (targeting.kind === "attack" || targeting.target === "enemy");
  const allyTargeting = targeting && targeting.kind !== "attack" && targeting.target === "ally";

  const fire = (target?: number) => {
    if (!sel) return;
    if (sel.kind === "attack") void act({ type: "attack", slot: sel.slot, target });
    else if (sel.kind === "skill") void act({ type: "skill", slot: sel.slot, target });
    else if (sel.kind === "ultimate") void act({ type: "ultimate", slot: sel.slot, target });
    else if (sel.kind === "fusion") void act({ type: "fusion", id: sel.id, target });
  };
  const needsTarget = (k: BattleSkill["target"] | "none"): Target | null => (k === "enemy" ? "enemy" : k === "ally" ? "ally" : null);
  const chooseSkill = (slot: number, a: NonNullable<BattleAnima>) => {
    if (!a.skill) return;
    const tg = needsTarget(a.skill.target);
    if (tg) setSel({ kind: "skill", slot, name: a.skill.name, target: tg }); else void act({ type: "skill", slot, target: slot });
  };
  const chooseUltimate = (slot: number, a: NonNullable<BattleAnima>) => {
    const tg = needsTarget(a.ultimate.target);
    if (tg) setSel({ kind: "ultimate", slot, name: a.ultimate.name, target: tg }); else void act({ type: "ultimate", slot, target: slot });
  };
  const chooseFusion = (f: BattleFusion) => {
    const tg = needsTarget(f.target);
    if (tg) setSel({ kind: "fusion", id: f.id, name: f.name, target: tg }); else void act({ type: "fusion", id: f.id });
  };

  const slotView = (a: BattleAnima, testid: string, onClick?: () => void, extra?: string) => (
    <button className={`btn slot ${extra ?? ""}`} data-testid={testid} disabled={busy || !onClick} onClick={onClick} style={{ minHeight: 96, flexDirection: "column", alignItems: "flex-start", textAlign: "left" }}>
      {a ? <>
        <b>{a.name}</b><small>{a.element}{a.skill ? ` · ${a.skill.name}${a.skill.cooldown > 0 ? ` (${t("battle.cd", { n: a.skill.cooldown })})` : ""}` : ""}</small>
        <small>ATK {a.atk} · DEF {a.def} · HP {a.hp}/{a.maxHp}{a.summoned ? " ✦" : ""}</small>
        {a.statuses.length > 0 && <small className="muted" data-testid="statuses">{a.statuses.map(statusLabel).join(" · ")}</small>}
      </> : <span className="muted">—</span>}
    </button>
  );

  const actor = sel && (sel.kind === "actor" || sel.kind === "attack" || sel.kind === "skill" || sel.kind === "ultimate") ? view.you.field[sel.slot] : null;

  return (
    <>
      <PageTitle sub={view.phase === "mulligan" ? t("battle.mulliganText") : myTurn ? t("battle.yourTurn") : t("battle.opponentTurn")}>{t("battle.title")}</PageTitle>
      {view.result && (
        <section className="panel" data-testid="battle-result" data-won={view.result.won}>
          <h2>{view.result.won ? t("battle.won") : t("battle.lost")}</h2><p>{t(`battle.reason.${view.result.reason}` as never)}</p>
          <button className="btn primary" data-testid="battle-back" onClick={() => { setView(null); void loadMenu(); }}>{t("battle.back")}</button>
        </section>
      )}

      <section className="panel grid" data-testid="battle-board">
        <div className="row wrap">
          <span className="chip" data-testid="battle-meter" title={t("battle.meterHint")}>{t("battle.meter", { n: view.meter })}</span>
          <div className="progress" style={{ flex: 1, minWidth: 120 }}><i style={{ width: `${view.meter}%` }} /></div>
          {(view.surge.you > 0 || view.surge.opponent > 0) && <span className="chip" data-testid="battle-surge">{t("battle.surge", { n: view.surge.you })}</span>}
        </div>
        <div className="row wrap"><b>{t("battle.opponent")}</b><span className="chip" data-testid="opp-keeper">{t("battle.keeper")} {n(view.opponent.keeper)}</span><span className="chip">{t("battle.handCount", { n: view.opponent.hand })}</span><span className="chip">{t("battle.deckCount", { n: view.opponent.deck })}</span></div>
        <div className="cols" style={{ gridTemplateColumns: "repeat(3, 1fr)" }}>
          {view.opponent.field.map((a, i) => <div key={i}>{slotView(a, `opp-slot-${i}`, enemyTargeting && a ? () => fire(i) : undefined)}</div>)}
        </div>
        {sel?.kind === "attack" && <button className="btn" data-testid="attack-keeper" disabled={busy || !oppEmpty} onClick={() => fire(-1)}>{t("battle.attackKeeper")}</button>}
        <hr />
        <div className="cols" style={{ gridTemplateColumns: "repeat(3, 1fr)" }}>
          {view.you.field.map((a, i) => (
            <div key={i}>{slotView(a, `my-slot-${i}`,
              myTurn && sel?.kind === "hand" && !a ? () => void act({ type: "play", hand: sel.index, slot: i })
                : myTurn && allyTargeting && a ? () => fire(i)
                : myTurn && !targeting && a?.canAttack ? () => setSel(sel?.kind === "actor" && sel.slot === i ? null : { kind: "actor", slot: i }) : undefined,
              sel && "slot" in sel && sel.slot === i ? "primary" : "")}</div>
          ))}
        </div>
        {sel?.kind === "actor" && actor && (
          <div className="row wrap" data-testid="actor-actions">
            <button className="btn primary" disabled={busy} data-testid="act-attack" onClick={() => setSel({ kind: "attack", slot: sel.slot })}>{t("battle.attack")}</button>
            {actor.skill?.kind === "active" && <button className="btn" disabled={busy || actor.skill.cooldown > 0} data-testid="act-skill" onClick={() => chooseSkill(sel.slot, actor)}>{t("battle.skill")}: {actor.skill.name}{actor.skill.cooldown > 0 ? ` (${t("battle.cd", { n: actor.skill.cooldown })})` : ""}</button>}
            <button className="btn" disabled={busy || view.meter < 50 || actor.feared || oppEmpty} data-testid="act-ultimate" onClick={() => chooseUltimate(sel.slot, actor)}>{t("battle.ultimate")}: {actor.ultimate.name}</button>
            <button className="btn ghost" onClick={() => setSel(null)}>{t("battle.cancel")}</button>
          </div>
        )}
        <div className="row wrap"><b>{t("battle.you")}</b><span className="chip" data-testid="my-keeper">{t("battle.keeper")} {n(view.you.keeper)}</span><span className="chip" data-testid="my-energy">{t("battle.energy", { n: view.you.energy })}</span><span className="chip">{t("battle.deckCount", { n: view.you.deck })}</span></div>
        {sel?.kind === "hand" && <p className="muted">{t("battle.hintHand")}</p>}
        {sel?.kind === "actor" && <p className="muted">{t("battle.hintActor")}</p>}
        {targeting && targeting.kind !== "attack" && <p className="muted">{t("battle.hintTarget", { skill: targeting.name })}</p>}
        {sel?.kind === "attack" && <p className="muted">{t("battle.hintAttack")}</p>}
      </section>

      <section className="panel grid"><h3>{t("battle.hand")}</h3>
        <div className="row wrap" data-testid="battle-hand">
          {view.you.hand.map((c) => (
            <button key={c.index} className={`btn ${sel?.kind === "hand" && sel.index === c.index ? "primary" : ""}`} data-testid="hand-card"
              disabled={busy || !myTurn || c.type !== "anima" || c.cost > view.you.energy} onClick={() => setSel(sel?.kind === "hand" && sel.index === c.index ? null : { kind: "hand", index: c.index })}
              style={{ flexDirection: "column", alignItems: "flex-start" }}>
              <b>{c.name}</b><small>{c.element} · {c.type === "anima" ? `✦${c.cost}` : t(`type.${c.type}` as never)}</small>{c.type === "anima" && <small>{c.atk}/{c.def}/{c.hp}</small>}
            </button>
          ))}
        </div>
        {myTurn && view.you.fusions.some((f) => f.available) && (
          <div data-testid="fusions"><h3>{t("battle.fusions")}</h3>
            <div className="row wrap">{view.you.fusions.filter((f) => f.available).map((f) => <button key={f.id} className="btn" disabled={busy} data-testid="fusion" onClick={() => chooseFusion(f)}>{f.name} <small className="muted">({f.elements.join(" + ")})</small></button>)}</div>
          </div>
        )}
        <div className="row wrap">
          {view.phase === "mulligan" && !view.you.decided && <>
            <button className="btn primary" disabled={busy} data-testid="battle-keep" onClick={() => void act({ type: "keep" })}>{t("battle.keep")}</button>
            {view.you.mulliganAvailable && <button className="btn" disabled={busy} data-testid="battle-mulligan" onClick={() => void act({ type: "mulligan" })}>{t("battle.mulligan")}</button>}
          </>}
          {myTurn && <button className="btn primary" disabled={busy} data-testid="battle-end" onClick={() => void act({ type: "end" })}>{t("battle.endTurn")}</button>}
          {view.status === "InProgress" && <button className="btn ghost" disabled={busy} data-testid="battle-concede" onClick={() => void act({ type: "concede" })}>{t("battle.concede")}</button>}
          {sel && <button className="btn ghost" onClick={() => setSel(null)}>{t("battle.cancel")}</button>}
        </div>
      </section>

      <section className="panel"><h3>{t("battle.log")}</h3>
        <ul className="muted" data-testid="battle-log" style={{ margin: 0, paddingLeft: 18 }}>{[...view.events].reverse().slice(0, 16).map((e) => <li key={e.seq}>{eventText(e)}</li>)}</ul>
      </section>
    </>
  );
}
