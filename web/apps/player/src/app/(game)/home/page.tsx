"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type OpenResult, type PackInstance, type Progress } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { PackOpening } from "@/components/PackOpening";

export default function HomePage() {
  const { t, refresh, fail } = useApp(); const { busy, run } = useAction();
  const [packs, setPacks] = useState<PackInstance[] | null>(null);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [opened, setOpened] = useState<OpenResult | null>(null);

  const load = useCallback(() => Promise.all([api.myPacks(), api.progress()]).then(([p, g]) => { setPacks(p); setProgress(g); }).catch(fail), [fail]);
  useEffect(() => { load(); }, [load]);
  const welcome = packs?.find((p) => p.kind === "welcome");

  return (
    <>
      <PageTitle>{t("home.hello")}</PageTitle>
      {welcome && (
        <section className="panel row wrap" aria-label={t("home.welcomeTitle")}>
          <div style={{ flex: 1, minWidth: 240 }}><h2>{t("home.welcomeTitle")}</h2><p>{t("home.welcomeText")}</p></div>
          <button className="btn primary" disabled={busy} data-testid="open-welcome" onClick={() => run(async () => { setOpened(await api.open(welcome.id)); })}>{t("home.openWelcome")}</button>
        </section>
      )}
      <div className="cols">
        <section className="panel"><h3>{t("home.unopened")}</h3><p style={{ fontSize: 28, fontWeight: 700, margin: 0 }} data-testid="unopened-count">{packs?.length ?? "…"}</p><div className="row" style={{ marginTop: 10 }}><Link className="btn" href="/packs">{t("home.goPacks")}</Link></div></section>
        <section className="panel"><h3>{t("home.progress")}</h3>{progress && (<><p data-testid="progress-text">{t("collection.progress", { owned: progress.owned, total: progress.total })}</p><div className="progress"><i style={{ width: `${progress.percent}%` }} /></div></>)}<div className="row" style={{ marginTop: 10 }}><Link className="btn" href="/collection">{t("nav.collection")}</Link></div></section>
        <section className="panel"><h3>{t("home.quick")}</h3><div className="row wrap"><Link className="btn" href="/store">{t("home.goStore")}</Link><Link className="btn" href="/forge">{t("nav.forge")}</Link></div></section>
      </div>
      {opened && <PackOpening title={t("packs.welcome")} cards={opened.cards} result={opened} onClose={() => { setOpened(null); load(); refresh(); }} />}
    </>
  );
}
