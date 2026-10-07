"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type OpenResult, type PackInstance } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";
import { PackOpening } from "@/components/PackOpening";

export default function PacksPage() {
  const { t, refresh, fail } = useApp(); const { busy, run } = useAction();
  const [packs, setPacks] = useState<PackInstance[] | null>(null);
  const [opened, setOpened] = useState<{ title: string; res: OpenResult } | null>(null);
  const load = useCallback(() => api.myPacks().then(setPacks).catch(fail), [fail]);
  useEffect(() => { load(); }, [load]);

  return (
    <>
      <PageTitle>{t("packs.title")}</PageTitle>
      {packs && packs.length === 0 && <div className="panel"><p>{t("packs.none")}</p><Link className="btn" href="/store">{t("home.goStore")}</Link></div>}
      <div className="grid" data-testid="pack-list">
        {packs?.map((p) => (
          <div key={p.id} className="panel row" data-testid="pack-item">
            <div className="pack-art" style={{ width: 56, cursor: "default", fontSize: 10 }} aria-hidden>ANIMA</div>
            <div style={{ flex: 1 }}>
              <h3>{p.kind === "welcome" ? t("packs.welcome") : t("packs.standard")}</h3>
              {p.soulbound && <span className="chip">{t("common.soulbound")}</span>}
            </div>
            <button className="btn primary" disabled={busy} data-testid="open-pack" onClick={() => run(async () => {
              const res = await api.open(p.id);
              setOpened({ title: p.kind === "welcome" ? t("packs.welcome") : t("packs.standard"), res });
            })}>{t("packs.open")}</button>
          </div>
        ))}
      </div>
      {opened && <PackOpening title={opened.title} cards={opened.res.cards} result={opened.res} onClose={() => { setOpened(null); load(); refresh(); }} />}
    </>
  );
}
