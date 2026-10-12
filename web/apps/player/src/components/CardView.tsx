"use client";
import type { Card, Rarity } from "@/lib/api";
import { useApp } from "@/lib/app";
import { rainbowTier, skillName } from "@/lib/skills";

export const HUE: Record<string, number> = { Luminara: 45, Umbryx: 265, Pyraxis: 12, Aqualis: 200, Terrakin: 95, Ventara: 165, Voltaris: 290, Nihilum: 0 };
export const GLYPH: Record<string, string> = { Luminara: "✦", Umbryx: "☾", Pyraxis: "♨", Aqualis: "≈", Terrakin: "▲", Ventara: "❋", Voltaris: "ϟ", Nihilum: "∅" };

type Props = { card: Card; rarity?: Rarity; edition?: string; soulbound?: boolean; flipped?: boolean; onClick?: () => void; size?: number; faceDownLabel?: string };

/**
 * Mặt thẻ theo thiết kế 8 hệ cảm xúc: khung và nền riêng từng hệ, tranh ở giữa, đủ chỉ số (Cộng hưởng, ATK, DEF, HP), kỹ năng, độ hiếm và số thứ tự bản.
 * Từ rarity Rare trở lên có viền cầu vồng 7 sắc (Rare tĩnh, Epic xoay chậm, Legendary xoay + phát sáng, Secret xoay nhanh + lấp lánh).
 * `flipped=false` hiện mặt sau (dùng khi mở pack).
 */
export function CardView({ card, rarity, edition, soulbound, flipped = true, onClick, size = 150 }: Props) {
  const { t } = useApp();
  const r = rarity ?? card.rarity;
  const tier = rainbowTier(r);
  const Tag = onClick ? "button" : "div";
  const skill = skillName(card.skill);
  const label = `${card.name} — ${t(`rarity.${r}` as const)}${card.type === "anima" ? ` — ATK ${card.atk}, DEF ${card.def}, HP ${card.hp}` : ""}`;
  return (
    <Tag className={`card rarity-${r} el-${card.element} ${tier ? `rainbow t${tier}` : ""} ${flipped ? "flipped" : ""}`} style={{ ["--w" as string]: `${size}px` }} onClick={onClick} aria-label={label} data-rarity={r} data-element={card.element}>
      <div className="inner">
        <div className="back">A</div>
        <div className="face">
          <div className="plate"><b className="nm">{card.name}</b><small className="emo">{card.element} · {t(`el.${card.element}` as never).split(" · ")[0]}</small></div>
          <div className="art" style={{ ["--art" as string]: `url(/art/${card.element.toLowerCase()}.webp)` }}>
            <span className="glyph" aria-hidden>{GLYPH[card.element]}</span>
            <i className="cost" title={t("card.cost" as never)}>{card.cost}</i>
            {soulbound && <span className="bound" title={t("common.soulbound")}>⚭</span>}
            {tier >= 4 && <span className="shimmer" aria-hidden />}
          </div>
          {card.type === "anima" ? (
            <div className="stats" data-testid="card-stats">
              <span title="ATK"><small>ATK</small><b>{card.atk}</b></span>
              <span title="DEF"><small>DEF</small><b>{card.def}</b></span>
              <span title="HP"><small>HP</small><b>{card.hp}</b></span>
            </div>
          ) : <div className="stats support"><span><small>{t(`type.${card.type}` as const)}</small></span></div>}
          {skill && <div className="skill" title={skill}>✦ {skill}</div>}
          <div className="meta"><span className="rarity-text">{t(`rarity.${r}` as const)}</span><span>{edition ?? ""}</span></div>
        </div>
      </div>
    </Tag>
  );
}
