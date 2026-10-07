"use client";
import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import type { OpenedCard, OpenResult } from "@/lib/api";
import { RARITIES } from "@/lib/api";
import { useApp } from "@/lib/app";
import { CardView } from "./CardView";

const idx = (r: string) => RARITIES.indexOf(r as (typeof RARITIES)[number]);
const reducedMotion = () => typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;

type Props = {
  cards: OpenedCard[]; title: string; onClose: () => void;
  /** Chỉ pack mới có pity và thông tin công bằng; thẻ rèn chỉ có một thẻ. */
  result?: Pick<OpenResult, "pityBefore" | "pityAfter" | "pityTriggered" | "fairness">;
};

/**
 * Mở pack theo 3 giai đoạn: Entry (pack) → Reveal (lật từng thẻ theo rarity tăng dần, kết quả đã do server quyết định) → Summary.
 * Skip luôn có (BR-PACK-08). Epic+ làm tối nền (Climax), Legendary+ nháy sáng và rung — tắt khi người dùng chọn giảm chuyển động (NFR-09).
 */
export function PackOpening({ cards, title, onClose, result }: Props) {
  const { t } = useApp();
  const [stage, setStage] = useState<"pack" | "reveal" | "summary">("pack");
  const [tearing, setTearing] = useState(false);
  const [i, setI] = useState(0);
  const [flipped, setFlipped] = useState(false);
  const [flash, setFlash] = useState(false);
  const cur = cards[i];
  const motion = useMemo(() => !reducedMotion(), []);
  const hi = cur ? idx(cur.rarity) : 0;

  const tear = () => { if (tearing) return; setTearing(true); setTimeout(() => { setStage("reveal"); setTearing(false); }, motion ? 600 : 0); };
  const flip = () => {
    if (flipped) return;
    setFlipped(true);
    if (motion && hi >= 4) { setFlash(true); setTimeout(() => setFlash(false), 600); }
  };
  const next = () => { if (i + 1 >= cards.length) setStage("summary"); else { setI(i + 1); setFlipped(false); } };
  useEffect(() => {
    const k = (e: KeyboardEvent) => { if (e.key === "Escape" && stage === "summary") onClose(); };
    window.addEventListener("keydown", k); return () => window.removeEventListener("keydown", k);
  }, [stage, onClose]);

  return (
    <div className={`overlay ${stage === "reveal" && flipped && hi >= 3 ? "dim" : ""}`} role="dialog" aria-modal="true" aria-label={title} data-testid="pack-opening" data-stage={stage}>
      {flash && <div className="flash" aria-hidden />}
      <div className={`dialog ${flash ? "shake" : ""}`}>
        {stage !== "summary" && <div className="row"><h2>{title}</h2><span className="spacer" /><button className="btn ghost sm" onClick={() => setStage("summary")} data-testid="skip">{t("common.skip")}</button></div>}

        {stage === "pack" && (
          <div className="stage">
            <button className={`pack-art ${tearing ? "tearing" : ""}`} onClick={tear} aria-label={t("open.tear")} data-testid="pack-art">
              ANIMA<small>{title}</small>
            </button>
            <p className="muted">{t("open.hint")}</p>
            <button className="btn primary" onClick={tear} data-testid="tear">{t("open.tear")}</button>
          </div>
        )}

        {stage === "reveal" && cur && (
          <div className={`stage ${flipped && hi >= 3 ? "climax" : ""}`} data-testid="reveal">
            <CardView card={cur.card} rarity={cur.rarity} edition={cur.edition} soulbound={cur.soulbound} flipped={flipped} onClick={flip} size={220} />
            {!flipped ? <p className="muted">{t("open.tapFlip")}</p> : (
              <div className="row"><span className="muted">{i + 1}/{cards.length}</span><button className="btn primary" onClick={next} data-testid="next">{i + 1 >= cards.length ? t("open.summary") : t("open.next")}</button></div>
            )}
            {!flipped && <button className="btn" onClick={flip} data-testid="flip">{t("open.tapFlip")}</button>}
          </div>
        )}

        {stage === "summary" && (
          <div className="grid" data-testid="summary">
            <h2>{t("open.summary")}</h2>
            <div className="card-grid">{cards.map((c) => <div key={c.instanceId} className="card-wrap"><CardView card={c.card} rarity={c.rarity} edition={c.edition} soulbound={c.soulbound} size={130} /></div>)}</div>
            {result && (
              <div className="note">
                <div>{t("open.pity", { a: result.pityBefore, b: result.pityAfter })}{result.pityTriggered && <b> · {t("open.pityHit")}</b>}</div>
                <div className="muted">{t("open.fairnessText", { hash: `${result.fairness.seedHash.slice(0, 12)}…`, client: result.fairness.clientSeed, nonce: result.fairness.nonce })}</div>
              </div>
            )}
            <div className="row end"><Link className="btn" href="/collection" onClick={onClose}>{t("open.goCollection")}</Link><button className="btn primary" onClick={onClose} data-testid="done">{t("open.done")}</button></div>
          </div>
        )}
      </div>
    </div>
  );
}
