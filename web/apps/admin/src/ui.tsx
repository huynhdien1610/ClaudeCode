import { useCallback, useEffect, useState, type ReactNode } from "react";
import { ApiError } from "./api.ts";

export const errText = (e: unknown) => (e instanceof ApiError ? `${e.code}: ${e.message}` : String(e));
export const fmtTime = (iso: string | null | undefined) => (iso ? new Date(iso).toLocaleString("vi-VN", { hour12: false }) : "—");
export const num = (n: number) => n.toLocaleString("vi-VN");

/** Tải dữ liệu một lần và cho phép nạp lại. */
export function useLoad<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const reload = useCallback(async () => {
    setLoading(true);
    try { setData(await fn()); setError(null); } catch (e) { setError(errText(e)); } finally { setLoading(false); }
  }, deps);
  useEffect(() => { void reload(); }, [reload]);
  return { data, error, loading, reload };
}

/** Chạy một hành động ghi; hiện lỗi/thành công ngay trên trang. */
export function useAct() {
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const run = async (fn: () => Promise<unknown>, okText: string, after?: () => void | Promise<void>) => {
    setBusy(true);
    try { await fn(); setMsg({ ok: true, text: okText }); await after?.(); } catch (e) { setMsg({ ok: false, text: errText(e) }); } finally { setBusy(false); }
  };
  return { msg, busy, run, clear: () => setMsg(null) };
}

export function Msg({ msg }: { msg: { ok: boolean; text: string } | null }) {
  return msg ? <div role={msg.ok ? "status" : "alert"} className={`notice ${msg.ok ? "ok" : "err"}`}>{msg.text}</div> : null;
}
export const ErrorBox = ({ error }: { error: string | null }) => (error ? <div role="alert" className="notice err">{error}</div> : null);

export function Status({ value }: { value: string }) {
  value = value === "DENIED" ? value : value.toLowerCase();
  const tone = ["active", "verified", "approved", "executed"].includes(value) ? "ok" : ["draft", "pending", "restricted", "unverified"].includes(value) ? "warn" : ["banned", "rejected", "denied", "DENIED", "deleted", "superseded"].includes(value) ? "bad" : "";
  return <span className={`chip ${tone}`}>{value}</span>;
}

export function Page({ title, hint, children, actions }: { title: string; hint?: string; children: ReactNode; actions?: ReactNode }) {
  return (
    <section>
      <div className="row wrap" style={{ marginBottom: 14 }}>
        <div><h1>{title}</h1>{hint && <p className="muted" style={{ margin: 0 }}>{hint}</p>}</div>
        <span className="spacer" />{actions}
      </div>
      <div className="grid">{children}</div>
    </section>
  );
}
