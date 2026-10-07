import { useEffect, useState, type ReactNode } from "react";
import { api, hasToken, setToken, setUnauthorizedHandler, type Me } from "./api.ts";
import { AREAS, canView, ROLE_LABEL, type Area } from "./perm.ts";
import { Accounts, Admins, AuditLog, Cards, Compensations, Dashboard, Economy, Odds } from "./pages.tsx";
import { errText } from "./ui.tsx";

const TITLE: Record<Area, string> = { dashboard: "Dashboard", accounts: "Tài khoản", odds: "Tỷ lệ rơi", economy: "Tham số kinh tế", cards: "Thẻ", compensations: "Bồi thường", audit: "Audit log", admins: "Quản trị viên" };

const areaFromHash = (): string => location.hash.replace(/^#\/?/, "");
function useHashArea(): [string, (a: Area) => void] {
  const [h, setH] = useState(areaFromHash());
  useEffect(() => { const f = () => setH(areaFromHash()); addEventListener("hashchange", f); return () => removeEventListener("hashchange", f); }, []);
  return [h, (a) => { location.hash = `/${a}`; }];
}

function Login({ onDone }: { onDone: (me: Me) => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setErr(null);
    try { const r = await api.login(email, password); setToken(r.accessToken); onDone(await api.me()); } catch (x) { setErr(errText(x)); } finally { setBusy(false); }
  };
  return (
    <div className="center">
      <form className="panel" style={{ width: 360 }} onSubmit={submit}>
        <div className="logo" style={{ marginBottom: 6 }}>ANIMA</div>
        <p className="muted">Trang quản trị nội bộ</p>
        <div className="grid">
          <label>Email<input aria-label="Email" type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} /></label>
          <label>Mật khẩu<input aria-label="Mật khẩu" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} /></label>
          {err && <div role="alert" className="notice err">{err}</div>}
          <button className="btn primary" disabled={busy || !email || !password}>Đăng nhập</button>
        </div>
      </form>
    </div>
  );
}

export function App() {
  const [me, setMe] = useState<Me | null>(null);
  const [booting, setBooting] = useState(hasToken());
  const [area, go] = useHashArea();

  useEffect(() => { setUnauthorizedHandler(() => { setToken(null); setMe(null); }); }, []);
  useEffect(() => { if (hasToken()) api.me().then(setMe).catch(() => setToken(null)).finally(() => setBooting(false)); }, []);

  if (booting) return null;
  if (!me) return <Login onDone={setMe} />;

  const allowed = AREAS.filter((a) => canView(me.role, a));
  const current = (allowed.find((a) => a === area) ?? allowed[0]) as Area | undefined;
  const logout = () => { setToken(null); setMe(null); location.hash = ""; };
  const pages: Record<Area, ReactNode> = {
    dashboard: <Dashboard />, accounts: <Accounts me={me} />, odds: <Odds me={me} />, economy: <Economy me={me} />,
    cards: <Cards me={me} />, compensations: <Compensations me={me} />, audit: <AuditLog />, admins: <Admins me={me} />,
  };
  return (
    <>
      <header className="topbar">
        <span className="logo">ANIMA</span><span className="chip">Admin</span>
        <nav className="row wrap" aria-label="Chính">
          {allowed.map((a) => <button key={a} className={`btn sm ${a === current ? "primary" : "ghost"}`} data-nav={a} onClick={() => go(a)}>{TITLE[a]}</button>)}
        </nav>
        <span className="spacer" />
        <span id="whoami" className="muted">{me.email} · {ROLE_LABEL[me.role] ?? me.role}</span>
        <button className="btn sm" onClick={logout}>Đăng xuất</button>
      </header>
      <main className="wrap-main">{current ? pages[current] : <p>Vai trò của bạn chưa có khu vực nào.</p>}</main>
    </>
  );
}
