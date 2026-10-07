// Quyền hiển thị theo vai trò — phản chiếu các Guard ở backend (backend vẫn là nơi quyết định, UI chỉ ẩn/hiện cho đỡ rối).
export const ROLES = ["cs_agent", "content_manager", "economy_manager", "fraud_analyst", "finance_viewer", "super_admin"] as const;
export type Role = (typeof ROLES)[number];

export const ROLE_LABEL: Record<string, string> = {
  cs_agent: "CS Agent", content_manager: "Content Manager", economy_manager: "Economy Manager",
  fraud_analyst: "Fraud Analyst", finance_viewer: "Finance Viewer", super_admin: "Super Admin",
};

export type Area = "dashboard" | "accounts" | "odds" | "economy" | "cards" | "compensations" | "audit" | "admins";

const VIEW: Record<Area, Role[]> = {
  dashboard: ["economy_manager", "finance_viewer", "super_admin"],
  accounts: ["cs_agent", "fraud_analyst", "super_admin"],
  odds: ["economy_manager", "finance_viewer", "super_admin"],
  economy: ["economy_manager", "finance_viewer", "super_admin"],
  cards: ["content_manager", "economy_manager", "super_admin"],
  compensations: ["cs_agent", "fraud_analyst"],
  audit: ["fraud_analyst", "super_admin"],
  admins: ["super_admin"],
};
export const AREAS = Object.keys(VIEW) as Area[];
export const canView = (role: string, area: Area) => (VIEW[area] as string[]).includes(role);

export type Action = "odds.draft" | "odds.approve" | "economy.propose" | "economy.decide" | "card.discontinue" | "pii.reveal" | "account.lock" | "account.unban" | "comp.create" | "comp.decide";
const DO: Record<Action, Role[]> = {
  "odds.draft": ["economy_manager"], "odds.approve": ["economy_manager", "super_admin"],
  "economy.propose": ["economy_manager"], "economy.decide": ["economy_manager", "super_admin"],
  "card.discontinue": ["content_manager"], "pii.reveal": ["fraud_analyst", "super_admin"],
  "account.lock": ["fraud_analyst", "super_admin"], "account.unban": ["super_admin"],
  "comp.create": ["cs_agent"], "comp.decide": ["fraud_analyst"],
};
export const can = (role: string, a: Action) => (DO[a] as string[]).includes(role);

/** Maker-checker: người tạo không được tự duyệt (BR-ADM-03). So khớp theo email không phân biệt hoa thường. */
export const isOwn = (me: string, creator: string | null | undefined) => !!creator && creator.toLowerCase() === me.toLowerCase();

export const RARITIES = ["common", "uncommon", "rare", "epic", "legendary", "secret"] as const;
export const PPM_TOTAL = 1_000_000;
export const sumPpm = (e: { ppm: number }[]) => e.reduce((s, x) => s + (Number.isFinite(x.ppm) ? x.ppm : 0), 0);
/** ppm → "12.3456%"; bỏ số 0 thừa. */
export const pct = (ppm: number) => `${(ppm / 10_000).toFixed(4).replace(/\.?0+$/, "")}%`;
/** "12.5" (phần trăm) → ppm; NaN nếu không hợp lệ. */
export const parsePct = (s: string) => { const n = Number(s.replace(",", ".")); return Number.isFinite(n) ? Math.round(n * 10_000) : NaN; };
