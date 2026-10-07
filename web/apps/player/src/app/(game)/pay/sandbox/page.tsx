"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { api, type PaymentOrder } from "@/lib/api";
import { useAction, useApp } from "@/lib/app";
import { PageTitle } from "@/components/Shell";

/** Trang thanh toán của cổng giả lập (dev/test). Cổng thật có trang riêng ở miền của họ; người chơi chỉ quay về /wallet. */
export default function SandboxPayPage() {
  const { t, n, fail, refresh, locale } = useApp(); const { busy, run } = useAction();
  const [order, setOrder] = useState<PaymentOrder | null>(null);
  useEffect(() => {
    const id = new URLSearchParams(window.location.search).get("order");
    if (id) api.order(id).then(setOrder).catch(fail);
  }, [fail]);

  const simulate = (outcome: "succeeded" | "failed") => order && run(async () => {
    const r = await api.simulatePayment(order.id, outcome);
    setOrder(r.order); await refresh();
  });
  const money = order ? new Intl.NumberFormat(locale, { style: "currency", currency: order.currency, maximumFractionDigits: 0 }).format(order.amountMinor) : "";

  return (
    <>
      <PageTitle sub={t("pay.sandboxNote")}>{t("pay.sandboxTitle")}</PageTitle>
      {order && (
        <section className="panel grid" data-testid="sandbox-order" data-status={order.status}>
          <h3>{t("pay.order")}</h3>
          <p><b className="cur gem">{n(order.gem)}</b> {t("common.gem")} · {money}</p>
          <p>{t("pay.status")}: <b data-testid="order-status">{t(`pay.state.${order.status}`)}</b></p>
          <div className="row wrap">
            {order.status === "Created" && <>
              <button className="btn primary" disabled={busy} data-testid="sandbox-pay" onClick={() => void simulate("succeeded")}>{t("pay.success")}</button>
              <button className="btn" disabled={busy} data-testid="sandbox-fail" onClick={() => void simulate("failed")}>{t("pay.fail")}</button>
            </>}
            <Link className="btn ghost" href="/wallet" data-testid="back-wallet">{t("pay.back")}</Link>
          </div>
        </section>
      )}
    </>
  );
}
