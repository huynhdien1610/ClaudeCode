"use client";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useApp } from "@/lib/app";

export default function Index() {
  const { ready, account, t } = useApp(); const router = useRouter();
  useEffect(() => { if (ready) router.replace(account ? "/home" : "/login"); }, [ready, account, router]);
  return <div className="center muted">{t("common.loading")}</div>;
}
