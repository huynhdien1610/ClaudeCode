"use client";
import { RARITIES } from "@/lib/api";
import { useApp } from "@/lib/app";

export function OddsTable({ entries }: { entries: { rarity: string; ppm: number }[] }) {
  const { t } = useApp();
  const sorted = [...entries].sort((a, b) => RARITIES.indexOf(a.rarity as never) - RARITIES.indexOf(b.rarity as never));
  return (
    <div className="odds" data-testid="odds">
      {sorted.map((e) => (
        <div key={e.rarity} className={`odds-row rarity-${e.rarity}`}>
          <span className="rarity-text">{t(`rarity.${e.rarity}` as never)}</span>
          <span className="odds-bar"><i style={{ width: `${e.ppm / 10000}%` }} /></span>
          <span>{(e.ppm / 10000).toFixed(e.ppm % 10000 === 0 ? 0 : 1)}%</span>
        </div>
      ))}
    </div>
  );
}
