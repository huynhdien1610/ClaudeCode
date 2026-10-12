"use client";
import { useCallback, useEffect, useState } from "react";
import { api, type MemoryOverview } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { skillName } from "@/lib/skills";

export default function MemoriesPage() {
  const { t, fail, notify, refresh } = useApp(); const { busy, run } = useAction();
  const [o, setO] = useState<MemoryOverview | null>(null);
  const load = useCallback(() => api.memories().then(setO).catch(fail), [fail]);
  useEffect(() => { void load(); }, [load]);

  const buy = (defId: number) => run(async () => { setO(await api.memoryBuy(defId)); notify("ok", t("memories.bought")); await refresh(); });
  if (!o) return <PageTitle sub={t("memories.sub")}>{t("memories.title")}</PageTitle>;
  const full = o.total >= o.max;

  return (
    <>
      <PageTitle sub={t("memories.sub")}>{t("memories.title")}</PageTitle>
      <section className="panel grid" data-testid="memory-stock">
        <h3>{t("memories.stock", { n: o.total, max: o.max })}</h3>
        {o.stock.length === 0 ? <p className="muted">{t("memories.empty")}</p> : (
          <ul style={{ margin: 0, paddingLeft: 18 }}>{o.stock.map((m) => <li key={m.defId}><b>{m.name}</b> ×{m.qty} <span className="chip">{m.element}</span> <small className="muted">{t("memories.passive", { skill: skillName(m.skill) ?? m.skill })}</small></li>)}</ul>
        )}
        <p className="muted">{o.canBuyThisWeek ? t("memories.weekly") : t("memories.boughtThisWeek")}</p>
      </section>
      <section className="panel grid"><h3>{t("memories.all")}</h3>
        <ul style={{ margin: 0, paddingLeft: 0, listStyle: "none", display: "grid", gap: 8 }}>
          {o.available.map((m) => (
            <li key={m.defId} className="row wrap" data-testid="memory-available">
              <span style={{ flex: 1 }}><b>{m.name}</b> <span className="chip">{m.element}</span> <small className="muted">{t("memories.passive", { skill: skillName(m.skill) ?? m.skill })}</small></span>
              <button className="btn sm" data-testid="memory-buy" disabled={busy || !o.canBuyThisWeek || full} onClick={() => buy(m.defId)}>{t("memories.buy", { price: o.gemPrice })}</button>
            </li>
          ))}
        </ul>
      </section>
    </>
  );
}
