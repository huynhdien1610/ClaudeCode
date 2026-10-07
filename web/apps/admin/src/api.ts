export class ApiError extends Error {
  constructor(public code: string, public status: number, message: string) { super(message); }
}

const KEY = "anima.admin.token";
let token: string | null = null;
try { token = sessionStorage.getItem(KEY); } catch { /* môi trường không có sessionStorage */ }
export const setToken = (t: string | null) => {
  token = t;
  try { t ? sessionStorage.setItem(KEY, t) : sessionStorage.removeItem(KEY); } catch { /* bỏ qua */ }
};
export const hasToken = () => token !== null;

let onUnauthorized: () => void = () => {};
export const setUnauthorizedHandler = (f: () => void) => { onUnauthorized = f; };

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`/admin/v1${path}`, {
      method,
      headers: { ...(body !== undefined ? { "Content-Type": "application/json" } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch { throw new ApiError("NETWORK_ERROR", 0, "Không kết nối được máy chủ"); }
  const text = await res.text();
  const json = text ? JSON.parse(text) : null;
  if (!res.ok) {
    const e = (json ?? {}) as { code?: string; message?: string };
    if (res.status === 401 && token) onUnauthorized();
    throw new ApiError(e.code ?? "INTERNAL_ERROR", res.status, e.message ?? res.statusText);
  }
  return json as T;
}
const get = <T,>(p: string) => request<T>("GET", p);
const post = <T,>(p: string, b?: unknown) => request<T>("POST", p, b ?? {});

export type Me = { id: string; email: string; role: string };
export type AdminUser = { id: string; email: string; role: string; active: boolean; createdAt: string };
export type AccountRow = { id: string; emailMasked: string; phoneMasked: string | null; status: string; restrictionReason: string | null; legalCountry: string; locale: string; createdAt: string };
export type AccountDetail = { account: { id: string; status: string; phoneVerified: boolean; locale: string; timezone: string; legalCountry: string; restrictionReason: string | null; createdAt: string }; balances: { gem: number; coin: number } };
export type Pii = { email: string; phone: string | null; birthDate: string };
export type Pack = { code: string; name: string; kind: string; onSale: boolean; offSaleReason: string | null };
export type OddsEntry = { rarity: string; ppm: number };
export type OddsVersion = { id: number; packCode: string; version: number; status: string; effectiveFrom: string; createdBy: string; approvedBy: string | null; entries: OddsEntry[]; inEffect: boolean };
export type EconParam = { key: string; value: number; version: number; effectiveFrom: string; createdBy: string; approvedBy: string; note: string | null };
export type EconChange = { id: string; key: string; value: number; effectiveFrom: string; proposedBy: string; proposedAt: string; note: string | null; status: string; decidedBy: string | null; decidedAt: string | null };
export type Card = { id: number; code: string; name: string; element: string; rarity: string; cardType: string; maxSupply: number; issued: number; burned: number; discontinued: boolean };
export type Compensation = { id: string; accountId: string; coin: number; ticket: string; reason: string | null; status: string; createdBy: string; createdAt: string; decidedBy: string | null; decidedAt: string | null };
export type Audit = { id: number; at: string; actorEmail: string; actorRole: string; action: string; targetType: string | null; targetId: string | null; before: unknown; after: unknown; reason: string | null; outcome: string };

export const api = {
  login: (email: string, password: string) => request<{ accessToken: string; admin: AdminUser }>("POST", "/auth/login", { email, password }),
  me: () => get<Me>("/me"),
  admins: () => get<AdminUser[]>("/admins"),
  createAdmin: (email: string, password: string, role: string) => post<AdminUser>("/admins", { email, password, role }),
  updateAdmin: (id: string, b: { role?: string; active?: boolean }) => request<AdminUser>("PUT", `/admins/${id}`, b),
  accounts: (q: string) => get<AccountRow[]>(`/accounts?q=${encodeURIComponent(q)}`),
  account: (id: string) => get<AccountDetail>(`/accounts/${id}`),
  revealPii: (id: string) => post<Pii>(`/accounts/${id}/reveal-pii`),
  setStatus: (id: string, action: string, reason: string) => post<unknown>(`/accounts/${id}/status`, { action, reason }),
  packs: () => get<Pack[]>("/packs"),
  odds: (code: string) => get<OddsVersion[]>(`/packs/${code}/odds`),
  createOdds: (code: string, entries: OddsEntry[], effectiveFrom?: string) => post<OddsVersion>(`/packs/${code}/odds`, { entries, effectiveFrom }),
  approveOdds: (code: string, v: number) => post<OddsVersion>(`/packs/${code}/odds/${v}/approve`),
  economy: () => get<{ parameters: EconParam[]; changes: EconChange[] }>("/economy"),
  propose: (key: string, value: number, note: string) => post<EconChange>("/economy/changes", { key, value, note: note || undefined }),
  decide: (id: string, ok: boolean) => post<EconChange>(`/economy/changes/${id}/${ok ? "approve" : "reject"}`),
  cards: () => get<Card[]>("/cards"),
  discontinue: (id: number) => post<unknown>(`/cards/${id}/discontinue`),
  compensations: () => get<Compensation[]>("/compensations"),
  createComp: (accountId: string, coin: number, ticket: string, reason: string) => post<Compensation>("/compensations", { accountId, coin, ticket, reason }),
  decideComp: (id: string, ok: boolean) => post<Compensation>(`/compensations/${id}/${ok ? "approve" : "reject"}`),
  dashboard: () => get<{ generatedAt: string; metrics: Record<string, number> }>("/dashboard"),
  audit: (action: string) => get<Audit[]>(`/audit?limit=100${action ? `&action=${encodeURIComponent(action)}` : ""}`),
};
