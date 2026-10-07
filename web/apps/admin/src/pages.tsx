import { useState } from "react";
import { api, type AccountRow, type Me, type OddsEntry, type Pii } from "./api.ts";
import { can, isOwn, parsePct, pct, PPM_TOTAL, RARITIES, ROLES, ROLE_LABEL, sumPpm } from "./perm.ts";
import { ErrorBox, fmtTime, Msg, num, Page, Status, useAct, useLoad } from "./ui.tsx";

type P = { me: Me };

// ---------- Dashboard ----------
const METRIC_LABEL: Record<string, string> = {
  coin_issued: "Coin đã phát", coin_spent: "Coin đã tiêu", gem_issued: "Gem đã phát (nạp/thưởng)", gem_spent: "Gem đã tiêu", gem_dev_topup: "Gem nạp thử (dev)", ledger_entries: "Số dòng ledger",
};
export function Dashboard() {
  const { data, error, reload } = useLoad(api.dashboard);
  const groups = new Map<string, [string, number][]>();
  for (const [k, v] of Object.entries(data?.metrics ?? {})) {
    const g = k.includes(".") ? k.split(".")[0]! : "other";
    groups.set(g, [...(groups.get(g) ?? []), [k, v]]);
  }
  return (
    <Page title="Dashboard" hint={data ? `Cập nhật ${fmtTime(data.generatedAt)}` : undefined} actions={<button className="btn sm" onClick={() => void reload()}>Làm mới</button>}>
      <ErrorBox error={error} />
      <div className="cols">
        {[...groups].map(([g, rows]) => (
          <div className="panel" key={g}>
            <h3 style={{ textTransform: "capitalize" }}>{g === "other" ? "Chung" : g}</h3>
            <table><tbody>{rows.map(([k, v]) => <tr key={k}><td>{METRIC_LABEL[k] ?? k.replace(`${g}.`, "")}</td><td className="num" data-metric={k}>{num(v)}</td></tr>)}</tbody></table>
          </div>
        ))}
      </div>
    </Page>
  );
}

// ---------- Tài khoản ----------
export function Accounts({ me }: P) {
  const [q, setQ] = useState("");
  const [rows, setRows] = useState<AccountRow[] | null>(null);
  const [sel, setSel] = useState<string | null>(null);
  const act = useAct();
  return (
    <Page title="Tài khoản người chơi" hint="Tìm theo email, số điện thoại (+84…) hoặc ID. Dữ liệu cá nhân được che; xem rõ sẽ bị ghi audit.">
      <form className="row wrap" onSubmit={(e) => { e.preventDefault(); void act.run(async () => setRows(await api.accounts(q)), "Đã tìm"); }}>
        <input aria-label="Từ khóa" placeholder="email / +84… / ID" value={q} onChange={(e) => setQ(e.target.value)} style={{ minWidth: 280 }} />
        <button className="btn primary" disabled={act.busy}>Tìm</button>
      </form>
      {act.msg && !act.msg.ok && <Msg msg={act.msg} />}
      {rows && (
        <div className="panel">
          <table>
            <thead><tr><th>Email</th><th>Điện thoại</th><th>Trạng thái</th><th>Quốc gia</th><th>Tạo lúc</th><th /></tr></thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} data-row="account">
                  <td>{r.emailMasked}</td><td>{r.phoneMasked ?? "—"}</td><td><Status value={r.status} /></td><td>{r.legalCountry}</td><td>{fmtTime(r.createdAt)}</td>
                  <td><button className="btn sm" onClick={() => setSel(r.id)}>Chi tiết</button></td>
                </tr>
              ))}
              {rows.length === 0 && <tr><td colSpan={6} className="muted">Không có kết quả</td></tr>}
            </tbody>
          </table>
        </div>
      )}
      {sel && <AccountDetail key={sel} id={sel} me={me} />}
    </Page>
  );
}

function AccountDetail({ id, me }: { id: string; me: Me } ) {
  const { data, error, reload } = useLoad(() => api.account(id), [id]);
  const [pii, setPii] = useState<Pii | null>(null);
  const [reason, setReason] = useState("");
  const act = useAct();
  if (error) return <ErrorBox error={error} />;
  if (!data) return null;
  const a = data.account;
  const lock = can(me.role, "account.lock");
  const st = a.status.toLowerCase();
  const set = (action: string, okText: string) => act.run(() => api.setStatus(id, action, reason), okText, async () => { setReason(""); await reload(); });
  return (
    <div className="panel" id="account-detail">
      <div className="row wrap"><h2>Chi tiết</h2><Status value={a.status} /><span className="spacer" />
        <span className="chip">Gem <b>{num(data.balances.gem)}</b></span><span className="chip">Coin <b>{num(data.balances.coin)}</b></span></div>
      <p className="mono">{a.id}</p>
      <table><tbody>
        <tr><th>Quốc gia / Múi giờ</th><td>{a.legalCountry} · {a.timezone}</td></tr>
        <tr><th>Ngôn ngữ</th><td>{a.locale}</td></tr>
        <tr><th>Xác minh SĐT</th><td>{a.phoneVerified ? "Đã xác minh" : "Chưa"}</td></tr>
        <tr><th>Lý do hạn chế</th><td>{a.restrictionReason ?? "—"}</td></tr>
        <tr><th>Dữ liệu cá nhân</th><td>
          {pii ? <span id="pii">{pii.email} · {pii.phone ?? "—"} · sinh {pii.birthDate}</span>
            : can(me.role, "pii.reveal") ? <button className="btn sm" onClick={() => void act.run(async () => setPii(await api.revealPii(id)), "Đã xem — hành động này được ghi vào audit")}>Xem rõ (ghi audit)</button>
            : <span className="muted">Vai trò của bạn không được xem</span>}
        </td></tr>
      </tbody></table>
      {lock && (
        <div style={{ marginTop: 12 }}>
          <label>Lý do (bắt buộc cho khóa/ban)<input aria-label="Lý do" value={reason} onChange={(e) => setReason(e.target.value)} /></label>
          <div className="row wrap" style={{ marginTop: 8 }}>
            <button className="btn" disabled={act.busy || !["unverified", "verified"].includes(st)} onClick={() => void set("restrict", "Đã hạn chế")}>Hạn chế</button>
            <button className="btn" disabled={act.busy || st !== "restricted"} onClick={() => void set("unrestrict", "Đã gỡ hạn chế")}>Gỡ hạn chế</button>
            <button className="btn danger" disabled={act.busy || ["banned", "deleted"].includes(st)} onClick={() => void set("ban", "Đã ban")}>Ban</button>
            {can(me.role, "account.unban") && <button className="btn" disabled={act.busy || st !== "banned"} onClick={() => void set("unban", "Đã gỡ ban")}>Gỡ ban</button>}
          </div>
        </div>
      )}
      <Msg msg={act.msg} />
    </div>
  );
}

// ---------- Tỷ lệ rơi ----------
export function Odds({ me }: P) {
  const packs = useLoad(api.packs);
  const [code, setCode] = useState("");
  const active = code || packs.data?.[0]?.code || "";
  return (
    <Page title="Tỷ lệ rơi" hint="Mỗi lần đổi là một version mới. Người soạn không được tự duyệt (maker-checker); version đã duyệt không sửa được.">
      <ErrorBox error={packs.error} />
      <div className="row wrap">
        <label>Pack<select aria-label="Pack" value={active} onChange={(e) => setCode(e.target.value)}>{packs.data?.map((p) => <option key={p.code} value={p.code}>{p.name} ({p.code}){p.onSale ? "" : " — ngừng bán"}</option>)}</select></label>
      </div>
      {active && <OddsPack key={active} code={active} me={me} />}
    </Page>
  );
}

function OddsPack({ code, me }: { code: string; me: Me }) {
  const { data, error, reload } = useLoad(() => api.odds(code), [code]);
  const act = useAct();
  const [draft, setDraft] = useState<Record<string, string> | null>(null);
  const startDraft = () => {
    const cur = data?.find((v) => v.inEffect) ?? data?.[0];
    setDraft(Object.fromEntries(RARITIES.map((r) => [r, pct(cur?.entries.find((e) => e.rarity === r)?.ppm ?? 0).replace("%", "")])));
  };
  const entries: OddsEntry[] = draft ? RARITIES.map((r) => ({ rarity: r, ppm: parsePct(draft[r] ?? "") })) : [];
  const total = sumPpm(entries);
  const valid = draft !== null && entries.every((e) => Number.isFinite(e.ppm) && e.ppm >= 0) && total === PPM_TOTAL;
  return (
    <>
      <ErrorBox error={error} />
      <div className="panel">
        <div className="row"><h3>Các version</h3><span className="spacer" />{can(me.role, "odds.draft") && !draft && <button className="btn sm primary" onClick={startDraft}>Soạn version mới</button>}</div>
        <table>
          <thead><tr><th>v</th><th>Trạng thái</th><th>Hiệu lực từ</th><th>Người tạo</th><th>Người duyệt</th><th>Tỷ lệ</th><th /></tr></thead>
          <tbody>
            {data?.map((v) => (
              <tr key={v.version} data-row="odds" data-version={v.version}>
                <td>{v.version}</td>
                <td><Status value={v.status} />{v.inEffect && <span className="chip ok" style={{ marginLeft: 6 }}>đang áp dụng</span>}</td>
                <td>{fmtTime(v.effectiveFrom)}</td><td>{v.createdBy}</td><td>{v.approvedBy ?? "—"}</td>
                <td className="mono">{v.entries.map((e) => `${e.rarity[0]!.toUpperCase()}${e.rarity.slice(1, 3)} ${pct(e.ppm)}`).join(" · ")}</td>
                <td>
                  {v.status === "draft" && can(me.role, "odds.approve") && (isOwn(me.email, v.createdBy)
                    ? <span className="muted" title="Cần người khác duyệt">Cần người khác duyệt</span>
                    : <button className="btn sm primary" disabled={act.busy} onClick={() => void act.run(() => api.approveOdds(code, v.version), `Đã duyệt v${v.version}`, reload)}>Duyệt</button>)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <Msg msg={act.msg} />
      </div>
      {draft && (
        <form className="panel" id="odds-draft" onSubmit={(e) => { e.preventDefault(); void act.run(() => api.createOdds(code, entries), "Đã tạo bản nháp — chờ người khác duyệt", async () => { setDraft(null); await reload(); }); }}>
          <h3>Bản nháp mới (nhập theo %)</h3>
          <div className="row wrap">
            {RARITIES.map((r) => (
              <label key={r}>{r}<input inputMode="decimal" aria-label={r} style={{ width: 110 }} value={draft[r] ?? ""} onChange={(e) => setDraft({ ...draft, [r]: e.target.value })} /></label>
            ))}
          </div>
          <p className={valid ? "muted" : "err-text"} id="odds-sum">Tổng: {Number.isFinite(total) ? pct(total) : "—"} {valid ? "✓" : "— phải đúng 100%"}</p>
          <div className="row"><button className="btn primary" disabled={!valid || act.busy}>Lưu bản nháp</button><button type="button" className="btn ghost" onClick={() => setDraft(null)}>Hủy</button></div>
        </form>
      )}
    </>
  );
}

// ---------- Tham số kinh tế ----------
const PARAM_KEYS = ["gem_to_coin_rate", "coin_to_gem_rate", "coin_to_gem_daily_cap_gem", "forge_fee_coin", "forge_fee_gem", "forge_daily_limit_unverified", "forge_daily_limit_verified", "first_login_coin", "pity_guarantee_after"];
export function Economy({ me }: P) {
  const { data, error, reload } = useLoad(api.economy);
  const act = useAct();
  const [key, setKey] = useState(PARAM_KEYS[0]!);
  const [value, setValue] = useState("");
  const [note, setNote] = useState("");
  const current = new Map<string, number>();
  for (const p of data?.parameters ?? []) if (!current.has(p.key)) current.set(p.key, p.value);
  return (
    <Page title="Tham số kinh tế" hint="Đề xuất → người khác duyệt → tạo version mới. Giá trị ngoài khoảng cho phép sẽ bị từ chối.">
      <ErrorBox error={error} />
      <div className="cols">
        <div className="panel">
          <h3>Giá trị hiện hành</h3>
          <table><tbody>{[...current].map(([k, v]) => <tr key={k}><td className="mono">{k}</td><td className="num" data-param={k}>{num(v)}</td></tr>)}</tbody></table>
        </div>
        {can(me.role, "economy.propose") && (
          <form className="panel" id="econ-form" onSubmit={(e) => { e.preventDefault(); void act.run(() => api.propose(key, Number(value), note), "Đã gửi đề xuất — chờ người khác duyệt", async () => { setValue(""); setNote(""); await reload(); }); }}>
            <h3>Đề xuất thay đổi</h3>
            <div className="grid">
              <label>Tham số<select aria-label="Tham số" value={key} onChange={(e) => setKey(e.target.value)}>{PARAM_KEYS.map((k) => <option key={k}>{k}</option>)}</select></label>
              <label>Giá trị mới (hiện tại {current.get(key) ?? "—"})<input aria-label="Giá trị mới" inputMode="numeric" value={value} onChange={(e) => setValue(e.target.value)} /></label>
              <label>Ghi chú<input aria-label="Ghi chú" value={note} onChange={(e) => setNote(e.target.value)} /></label>
              <button className="btn primary" disabled={act.busy || !/^\d+$/.test(value)}>Gửi đề xuất</button>
            </div>
          </form>
        )}
      </div>
      <Msg msg={act.msg} />
      <div className="panel">
        <h3>Đề xuất</h3>
        <table>
          <thead><tr><th>Tham số</th><th>Giá trị</th><th>Người đề xuất</th><th>Lúc</th><th>Trạng thái</th><th>Người quyết</th><th /></tr></thead>
          <tbody>
            {data?.changes.map((c) => (
              <tr key={c.id} data-row="change" data-key={c.key}>
                <td className="mono">{c.key}</td><td>{num(c.value)}</td><td>{c.proposedBy}</td><td>{fmtTime(c.proposedAt)}</td><td><Status value={c.status} /></td><td>{c.decidedBy ?? "—"}</td>
                <td className="row">
                  {c.status === "pending" && can(me.role, "economy.decide") && (isOwn(me.email, c.proposedBy)
                    ? <span className="muted">Cần người khác duyệt</span>
                    : <><button className="btn sm primary" disabled={act.busy} onClick={() => void act.run(() => api.decide(c.id, true), "Đã duyệt", reload)}>Duyệt</button>
                      <button className="btn sm" disabled={act.busy} onClick={() => void act.run(() => api.decide(c.id, false), "Đã từ chối", reload)}>Từ chối</button></>)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Page>
  );
}

// ---------- Thẻ ----------
export function Cards({ me }: P) {
  const { data, error, reload } = useLoad(api.cards);
  const act = useAct();
  const [filter, setFilter] = useState("");
  const rows = (data ?? []).filter((c) => !filter || c.rarity === filter);
  return (
    <Page title="Thẻ" hint="Thẻ đã phát hành không xóa được — chỉ ngừng phát hành (không cấp mới, bản đã có giữ nguyên).">
      <ErrorBox error={error} /><Msg msg={act.msg} />
      <label style={{ maxWidth: 200 }}>Độ hiếm<select aria-label="Độ hiếm" value={filter} onChange={(e) => setFilter(e.target.value)}><option value="">Tất cả</option>{RARITIES.map((r) => <option key={r}>{r}</option>)}</select></label>
      <div className="panel">
        <table>
          <thead><tr><th>ID</th><th>Mã</th><th>Tên</th><th>Hệ</th><th>Hiếm</th><th>Phát hành</th><th>Đã rèn</th><th /></tr></thead>
          <tbody>
            {rows.map((c) => (
              <tr key={c.id} data-row="card">
                <td>{c.id}</td><td className="mono">{c.code}</td><td>{c.name}</td><td>{c.element}</td><td>{c.rarity}</td><td>{c.issued}/{c.maxSupply}</td><td>{c.burned}</td>
                <td>{c.discontinued ? <span className="chip bad">đã ngừng</span> : can(me.role, "card.discontinue") && <button className="btn sm" disabled={act.busy} onClick={() => void act.run(() => api.discontinue(c.id), `Đã ngừng phát hành ${c.name}`, reload)}>Ngừng phát hành</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Page>
  );
}

// ---------- Bồi thường ----------
export function Compensations({ me }: P) {
  const { data, error, reload } = useLoad(api.compensations);
  const act = useAct();
  const [f, setF] = useState({ accountId: "", coin: "", ticket: "", reason: "" });
  const ok = /^[0-9a-f-]{36}$/i.test(f.accountId) && /^\d+$/.test(f.coin) && Number(f.coin) > 0 && f.ticket.trim() !== "";
  return (
    <Page title="Bồi thường Coin" hint="Chỉ bồi thường bằng Coin, có mã ticket. CS tạo — Fraud Analyst duyệt. Admin không bao giờ cộng Gem trực tiếp.">
      <ErrorBox error={error} />
      {can(me.role, "comp.create") && (
        <form className="panel" id="comp-form" onSubmit={(e) => { e.preventDefault(); void act.run(() => api.createComp(f.accountId.trim(), Number(f.coin), f.ticket.trim(), f.reason), "Đã tạo yêu cầu — chờ duyệt", async () => { setF({ accountId: "", coin: "", ticket: "", reason: "" }); await reload(); }); }}>
          <h3>Tạo yêu cầu</h3>
          <div className="row wrap">
            <label>ID tài khoản<input aria-label="ID tài khoản" style={{ width: 320 }} value={f.accountId} onChange={(e) => setF({ ...f, accountId: e.target.value })} /></label>
            <label>Coin<input aria-label="Coin" inputMode="numeric" style={{ width: 110 }} value={f.coin} onChange={(e) => setF({ ...f, coin: e.target.value })} /></label>
            <label>Ticket<input aria-label="Ticket" style={{ width: 150 }} value={f.ticket} onChange={(e) => setF({ ...f, ticket: e.target.value })} /></label>
            <label>Lý do<input aria-label="Lý do" style={{ width: 240 }} value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} /></label>
            <button className="btn primary" style={{ alignSelf: "end" }} disabled={!ok || act.busy}>Tạo</button>
          </div>
        </form>
      )}
      <Msg msg={act.msg} />
      <div className="panel">
        <table>
          <thead><tr><th>Ticket</th><th>Tài khoản</th><th>Coin</th><th>Người tạo</th><th>Trạng thái</th><th>Người duyệt</th><th /></tr></thead>
          <tbody>
            {data?.map((c) => (
              <tr key={c.id} data-row="comp" data-ticket={c.ticket}>
                <td>{c.ticket}</td><td className="mono">{c.accountId.slice(0, 8)}…</td><td>{num(c.coin)}</td><td>{c.createdBy}</td><td><Status value={c.status} /></td><td>{c.decidedBy ?? "—"}</td>
                <td className="row">
                  {c.status === "pending" && can(me.role, "comp.decide") && (isOwn(me.email, c.createdBy)
                    ? <span className="muted">Cần người khác duyệt</span>
                    : <><button className="btn sm primary" disabled={act.busy} onClick={() => void act.run(() => api.decideComp(c.id, true), "Đã duyệt và cộng Coin", reload)}>Duyệt</button>
                      <button className="btn sm" disabled={act.busy} onClick={() => void act.run(() => api.decideComp(c.id, false), "Đã từ chối", reload)}>Từ chối</button></>)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Page>
  );
}

// ---------- Audit ----------
export function AuditLog() {
  const [action, setAction] = useState("");
  const { data, error, reload } = useLoad(() => api.audit(action), [action]);
  return (
    <Page title="Audit log" hint="Chỉ thêm, không sửa/xóa được. Mọi hành động nhạy cảm và mọi lần bị từ chối đều có dòng ở đây." actions={<button className="btn sm" onClick={() => void reload()}>Làm mới</button>}>
      <ErrorBox error={error} />
      <label style={{ maxWidth: 260 }}>Lọc theo hành động<input aria-label="Lọc hành động" placeholder="vd. BAN, PII_VIEW" value={action} onChange={(e) => setAction(e.target.value.trim().toUpperCase())} /></label>
      <div className="panel" style={{ overflowX: "auto" }}>
        <table>
          <thead><tr><th>#</th><th>Lúc</th><th>Người thực hiện</th><th>Hành động</th><th>Đối tượng</th><th>Kết quả</th><th>Lý do</th></tr></thead>
          <tbody>
            {data?.map((a) => (
              <tr key={a.id} data-row="audit" data-action={a.action}>
                <td>{a.id}</td><td>{fmtTime(a.at)}</td><td>{a.actorEmail}<div className="muted">{a.actorRole}</div></td><td className="mono">{a.action}</td>
                <td className="mono">{a.targetType ? `${a.targetType}:${a.targetId ?? ""}` : "—"}</td><td><Status value={a.outcome} /></td><td>{a.reason ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Page>
  );
}

// ---------- Quản trị viên ----------
export function Admins({ me }: P) {
  const { data, error, reload } = useLoad(api.admins);
  const act = useAct();
  const [f, setF] = useState({ email: "", password: "", role: ROLES[0] as string });
  return (
    <Page title="Quản trị viên" hint="Chỉ Super Admin. Không tự đổi vai trò của mình; khóa admin thì token mất hiệu lực ngay.">
      <ErrorBox error={error} />
      <form className="panel" onSubmit={(e) => { e.preventDefault(); void act.run(() => api.createAdmin(f.email, f.password, f.role), "Đã tạo admin", async () => { setF({ ...f, email: "", password: "" }); await reload(); }); }}>
        <h3>Thêm admin</h3>
        <div className="row wrap">
          <label>Email<input aria-label="Email admin" type="email" value={f.email} onChange={(e) => setF({ ...f, email: e.target.value })} /></label>
          <label>Mật khẩu (≥ 12 ký tự)<input aria-label="Mật khẩu admin" type="password" autoComplete="new-password" value={f.password} onChange={(e) => setF({ ...f, password: e.target.value })} /></label>
          <label>Vai trò<select aria-label="Vai trò" value={f.role} onChange={(e) => setF({ ...f, role: e.target.value })}>{ROLES.map((r) => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}</select></label>
          <button className="btn primary" style={{ alignSelf: "end" }} disabled={act.busy || f.password.length < 12 || !f.email.includes("@")}>Tạo</button>
        </div>
      </form>
      <Msg msg={act.msg} />
      <div className="panel">
        <table>
          <thead><tr><th>Email</th><th>Vai trò</th><th>Trạng thái</th><th>Tạo lúc</th><th /></tr></thead>
          <tbody>
            {data?.map((a) => (
              <tr key={a.id} data-row="admin">
                <td>{a.email}{a.email === me.email && <span className="chip" style={{ marginLeft: 6 }}>bạn</span>}</td><td>{ROLE_LABEL[a.role] ?? a.role}</td>
                <td><Status value={a.active ? "active" : "disabled"} /></td><td>{fmtTime(a.createdAt)}</td>
                <td>{a.email !== me.email && <button className="btn sm" disabled={act.busy} onClick={() => void act.run(() => api.updateAdmin(a.id, { active: !a.active }), a.active ? "Đã khóa" : "Đã mở khóa", reload)}>{a.active ? "Khóa" : "Mở khóa"}</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Page>
  );
}
