export class ApiError extends Error {
  constructor(public code: string, public status: number, message: string, public details?: unknown) { super(message); }
}

const TOKEN_KEY = "anima.token";
export const tokenStore = {
  get: () => (typeof window === "undefined" ? null : window.localStorage.getItem(TOKEN_KEY)),
  set: (t: string) => window.localStorage.setItem(TOKEN_KEY, t),
  clear: () => window.localStorage.removeItem(TOKEN_KEY),
};

export const newKey = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`);

type Opts = { idem?: boolean | string; signal?: AbortSignal; keepalive?: boolean };

async function request<T>(method: string, path: string, body?: unknown, opts: Opts = {}): Promise<T> {
  const headers: Record<string, string> = {};
  const token = tokenStore.get();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (opts.idem) headers["Idempotency-Key"] = typeof opts.idem === "string" ? opts.idem : newKey();
  let res: Response;
  try {
    res = await fetch(`/api${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body), signal: opts.signal, keepalive: opts.keepalive });
  } catch {
    throw new ApiError("NETWORK_ERROR", 0, "Network error");
  }
  if (res.status === 204 || res.status === 202) return undefined as T;
  const text = await res.text();
  const json = text ? safeJson(text) : undefined;
  if (!res.ok) {
    const e = (json ?? {}) as { code?: string; message?: string; details?: unknown };
    throw new ApiError(e.code ?? "INTERNAL_ERROR", res.status, e.message ?? res.statusText, e.details);
  }
  return json as T;
}

function safeJson(t: string): unknown { try { return JSON.parse(t); } catch { return undefined; } }

// ---------- Kiểu dữ liệu (khớp contracts/openapi/anima.v1.json) ----------
export type Rarity = "common" | "uncommon" | "rare" | "epic" | "legendary" | "secret";
export const RARITIES: Rarity[] = ["common", "uncommon", "rare", "epic", "legendary", "secret"];
export type Element = "Umbryx" | "Pyraxis" | "Aqualis" | "Terrakin" | "Ventara" | "Voltaris" | "Luminara" | "Nihilum";

export type Account = { id: string; status: string; phoneVerified: boolean; locale: string; timezone: string; legalCountry: string; restrictionReason: string | null; createdAt: string };
export type AuthResponse = { accessToken: string; expiresAt: string; account: Account };
export type Balances = { gem: number; coin: number };
export type LedgerEntry = { id: number; currency: "GEM" | "COIN"; amount: number; balanceAfter: number; reason: string; refType: string | null; refId: string | null; createdAt: string };

export type Card = {
  id: number; code: string; name: string; epithet: string | null; story: string | null; season: string; element: Element; rarity: Rarity; type: "anima" | "echo" | "seal";
  cost: number; atk: number | null; def: number | null; hp: number | null; skill: string | null; arc: string | null;
  supply: { max: number; issued: number; burned: number; remaining: number };
};
export type OwnedCard = { id: string; serial: number; edition: string; editionNo: number; state: string; soulbound: boolean; origin: string; acquiredAt: string; card: Card };
export type Collection = { total: number; items: OwnedCard[] };
export type Progress = { owned: number; total: number; percent: number };

export type OddsEntry = { rarity: Rarity; ppm: number };
export type PackView = { pack: { code: string; name: string; kind: string; priceCoin: number | null; priceGem: number | null; cardsPerPack: number; onSale: boolean; offSaleReason: string | null }; odds: { version: number; entries: OddsEntry[] } | null };
export type PackInstance = { id: string; packCode: string; kind: "standard" | "welcome" | "basic"; soulbound: boolean; createdAt: string };
export type PurchaseResult = { packInstanceIds: string[]; currency: string; price: number; balances: Balances };
export type OpenedCard = { instanceId: string; serial: number; edition: string; editionNo: number; rarity: Rarity; soulbound: boolean; card: Card };
export type FairnessRef = { seedHash: string; clientSeed: string; nonce: number };
export type OpenResult = { packInstanceId: string; packCode: string; cards: OpenedCard[]; maxRarity: Rarity; climax: boolean; pityBefore: number; pityAfter: number; pityTriggered: boolean; fairness: FairnessRef };

export type ForgeInfo = { feeCoin: number; feeGem: number; dailyLimit: number; usedToday: number; odds: { version: number; entries: OddsEntry[] } };
export type SealedCard = { id: string; createdAt: string };
export type RevealResult = { sealedCardId: string; card: OpenedCard; fairness: FairnessRef & { rarityRoll: number; cardRoll: number; candidates: number } };

export type SeedPublic = { serverSeedHash: string; clientSeed: string; nextNonce: number };
export type RevealedSeed = { serverSeedHash: string; serverSeed: string; clientSeed: string; noncesUsed: number; revealedAt: string };
export type VerifyResult = { serverSeedHash: string; rolls: number[]; cardRolls: number[] };

export type QuestTask = { code: string; kind: "OPEN_PACK" | "VERIFY_PHONE"; target: number; progress: number; done: boolean };
export type QuestDay = { day: number; available: boolean; complete: boolean; claimed: boolean; rewardType: "BASIC_PACK" | "COIN"; rewardCoin: number; tasks: QuestTask[] };
export type Quests = { currentDay: number; expired: boolean; endsAt: string; days: QuestDay[] };
export type QuestClaim = { day: number; rewardType: "BASIC_PACK" | "COIN"; rewardCoin: number; packInstanceId: string | null; balances: Balances };

export type GemPackage = { code: string; gem: number; priceMinor: number; currency: string };
export type PaymentOrder = { id: string; packageCode: string; gem: number; amountMinor: number; currency: string; status: "Created" | "Paid" | "Failed" | "Refunded"; createdAt: string; paidAt: string | null; refundedAt: string | null };

export type DeckView = { id: string; name: string; isDefault: boolean; valid: boolean; violations: { rule: string; message: string }[]; cardInstanceIds: string[]; count: number; updatedAt: string };

export const api = {
  register: (b: { email: string; password: string; birthDate: string; country: string; locale: string; timezone: string }) => request<AuthResponse>("POST", "/v1/accounts", b),
  login: (b: { email: string; password: string }) => request<AuthResponse>("POST", "/v1/auth/login", b),
  me: () => request<Account>("GET", "/v1/me"),
  // keepalive: không bị huỷ khi người dùng đổi ngôn ngữ rồi chuyển trang ngay
  setLocale: (locale: string) => request<void>("PUT", "/v1/me/locale", { locale }, { keepalive: true }),
  sendOtp: (phone: string) => request<void>("POST", "/v1/me/phone/otp", { phone }, { idem: true }),
  verifyOtp: (code: string) => request<Account>("POST", "/v1/me/phone/verify", { code }, { idem: true }),

  wallet: () => request<Balances>("GET", "/v1/wallet"),
  ledger: (before?: number) => request<LedgerEntry[]>("GET", `/v1/wallet/ledger?limit=30${before ? `&before=${before}` : ""}`),
  convert: (direction: "GEM_TO_COIN" | "COIN_TO_GEM", amount: number) => request<{ spent: number; received: number; balances: Balances }>("POST", "/v1/wallet/convert", { direction, amount }, { idem: true }),
  gemPackages: () => request<GemPackage[]>("GET", "/v1/payments/packages"),
  createOrder: (packageCode: string) => request<{ order: PaymentOrder; checkoutUrl: string }>("POST", "/v1/payments/orders", { packageCode }, { idem: true }),
  order: (id: string) => request<PaymentOrder>("GET", `/v1/payments/orders/${id}`),
  /** Chỉ sandbox: mô phỏng cổng gửi IPN cho đơn của mình. */
  simulatePayment: (id: string, outcome: "succeeded" | "failed" | "refunded") => request<{ outcome: string; order: PaymentOrder }>("POST", `/v1/payments/sandbox/${id}/simulate`, { outcome }),
  devTopUp: (gem: number) => request<Balances>("POST", "/v1/dev/topup", { gem }, { idem: true }),
  economy: () => request<Record<string, number>>("GET", "/v1/economy"),

  packs: () => request<PackView[]>("GET", "/v1/packs"),
  buy: (code: string, currency: "GEM" | "COIN", quantity: number, key: string) => request<PurchaseResult>("POST", `/v1/packs/${code}/purchase`, { currency, quantity }, { idem: key }),
  quests: () => request<Quests>("GET", "/v1/me/quests"),
  claimQuest: (day: number) => request<QuestClaim>("POST", `/v1/me/quests/${day}/claim`, {}),
  myPacks: () => request<PackInstance[]>("GET", "/v1/me/packs"),
  open: (id: string) => request<OpenResult>("POST", `/v1/pack-instances/${id}/open`),
  pity: (code: string) => request<{ count: number; guaranteeAfter: number }>("GET", `/v1/me/pity/${code}`),

  collection: (q: { element?: string; rarity?: string; type?: string; q?: string }) => {
    const p = new URLSearchParams(); Object.entries(q).forEach(([k, v]) => v && p.set(k, v));
    return request<Collection>("GET", `/v1/collection?${p}`);
  },
  decks: () => request<DeckView[]>("GET", "/v1/decks"),
  createDeck: (name: string, cardInstanceIds: string[]) => request<DeckView>("POST", "/v1/decks", { name, cardInstanceIds }),
  saveDeck: (id: string, name: string, cardInstanceIds: string[]) => request<DeckView>("PUT", `/v1/decks/${id}`, { name, cardInstanceIds }),
  deleteDeck: (id: string) => request<void>("DELETE", `/v1/decks/${id}`),
  defaultDeck: (id: string) => request<DeckView>("POST", `/v1/decks/${id}/default`, {}),
  progress: () => request<Progress>("GET", "/v1/collection/progress"),
  card: (id: number, locale: string) => request<Card>("GET", `/v1/cards/${id}?locale=${locale}`),

  forgeInfo: () => request<ForgeInfo>("GET", "/v1/forge/info"),
  forge: (ids: string[], feeCurrency: "GEM" | "COIN", key: string) => request<{ sealedCardId: string; fee: number; balances: Balances }>("POST", "/v1/forge", { cardInstanceIds: ids, feeCurrency }, { idem: key }),
  sealed: () => request<SealedCard[]>("GET", "/v1/me/sealed"),
  reveal: (id: string) => request<RevealResult>("POST", `/v1/forge/sealed/${id}/reveal`),

  fairness: () => request<SeedPublic>("GET", "/v1/fairness"),
  setClientSeed: (clientSeed: string) => request<SeedPublic>("PUT", "/v1/fairness/client-seed", { clientSeed }),
  rotate: (clientSeed?: string) => request<{ previous: RevealedSeed; current: SeedPublic }>("POST", "/v1/fairness/rotate", clientSeed ? { clientSeed } : {}),
  fairnessHistory: () => request<RevealedSeed[]>("GET", "/v1/fairness/history"),
  verify: (b: { serverSeed: string; clientSeed: string; nonce: number; slots: number }) => request<VerifyResult>("POST", "/v1/fairness/verify", b),
};
