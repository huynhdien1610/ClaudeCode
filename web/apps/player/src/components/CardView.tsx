"use client";
import type { Card, Rarity } from "@/lib/api";
import { useApp } from "@/lib/app";

export const HUE: Record<string, number> = { Luminara: 45, Umbryx: 265, Pyraxis: 12, Aqualis: 200, Terrakin: 95, Ventara: 165, Voltaris: 290, Nihilum: 0 };
export const GLYPH: Record<string, string> = { Luminara: "✦", Umbryx: "☾", Pyraxis: "♨", Aqualis: "≈", Terrakin: "▲", Ventara: "❋", Voltaris: "ϟ", Nihilum: "∅" };

type Props = { card: Card; rarity?: Rarity; edition?: string; soulbound?: boolean; flipped?: boolean; onClick?: () => void; size?: number; faceDownLabel?: string };

/** Mặt thẻ. `flipped=false` hiện mặt sau (dùng khi mở pack). Màu viền theo rarity, nền theo hệ. */
export function CardView({ card, rarity, edition, soulbound, flipped = true, onClick, size = 150 }: Props) {
  const { t } = useApp();
  const r = rarity ?? card.rarity;
  const Tag = onClick ? "button" : "div";
  return (
    <Tag className={`card rarity-${r} ${flipped ? "flipped" : ""}`} style={{ ["--w" as string]: `${size}px`, ["--hue" as string]: HUE[card.element] }} onClick={onClick} aria-label={`${card.name} — ${t(`rarity.${r}` as const)}`}>
      <div className="inner">
        <div className="back">A</div>
        <div className="face">
          <div className="art"><span aria-hidden>{GLYPH[card.element]}</span><i className="cost">{card.cost}</i></div>
          <div className="name">{card.name}</div>
          {card.epithet && <div className="epithet">{card.epithet}</div>}
          {card.type === "anima" && <div className="stats"><b>{card.atk}</b>/<b>{card.def}</b>/<b>{card.hp}</b></div>}
          {card.type !== "anima" && <div className="stats muted">{t(`type.${card.type}` as const)}</div>}
          <div className="meta"><span className="rarity-text">{t(`rarity.${r}` as const)}</span><span>{edition ?? ""}</span></div>
          {soulbound && <span className="bound" title={t("common.soulbound")}>⚭</span>}
          <div className="band" />
        </div>
      </div>
    </Tag>
  );
}
