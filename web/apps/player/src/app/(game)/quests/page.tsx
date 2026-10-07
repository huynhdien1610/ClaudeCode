"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type QuestDay, type Quests } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";

export default function QuestsPage() {
  const { t, n, fail, notify, setBalances, refresh } = useApp(); const { busy, run } = useAction();
  const [q, setQ] = useState<Quests | null>(null);
  const load = useCallback(() => api.quests().then(setQ).catch(fail), [fail]);
  useEffect(() => { load(); }, [load]);

  const reward = (d: QuestDay) => (d.rewardType === "COIN" ? t("quests.rewardCoin", { n: n(d.rewardCoin) }) : t("quests.rewardPack"));
  const claim = (d: QuestDay) => run(async () => {
    const r = await api.claimQuest(d.day);
    setBalances(r.balances); notify("ok", t("quests.claimedOk", { r: reward(d) })); await load(); refresh();
  });

  return (
    <>
      <PageTitle sub={t("quests.sub")}>{t("quests.title")}</PageTitle>
      {q?.expired && <div className="panel"><p>{t("quests.expired")}</p></div>}
      <div className="grid" data-testid="quest-days">
        {q?.days.map((d) => (
          <section key={d.day} className="panel" data-testid="quest-day" data-day={d.day} data-state={d.claimed ? "claimed" : d.complete ? "ready" : "open"}>
            <div className="row wrap">
              <h3>{t("quests.day", { n: d.day })}</h3>
              {d.day === q.currentDay && !q.expired && <span className="chip">{t("quests.today")}</span>}
              {!d.available && !d.claimed && !q.expired && <span className="chip">{t("quests.locked")}</span>}
              <span className="spacer" />
              <span className="muted">{reward(d)}</span>
            </div>
            <ul style={{ margin: "8px 0", paddingLeft: 18 }}>
              {d.tasks.map((k) => (
                <li key={k.code} className={k.done ? "" : "muted"}>
                  {k.done ? "✓ " : "○ "}{t(`quests.task.${k.kind}`, { n: k.target })} <small>({t("quests.progress", { a: k.progress, b: k.target })})</small>
                </li>
              ))}
            </ul>
            <div className="row end" style={{ marginTop: 0 }}>
              {d.claimed ? <span className="chip">{t("quests.claimed")}</span>
                : <button className="btn primary" data-testid="claim-quest" disabled={busy || !d.available || !d.complete || q.expired} onClick={() => void claim(d)}>{t("quests.claim")}</button>}
            </div>
          </section>
        ))}
      </div>
      {!q?.expired && <p className="muted"><Link href="/packs">{t("nav.packs")}</Link></p>}
    </>
  );
}
