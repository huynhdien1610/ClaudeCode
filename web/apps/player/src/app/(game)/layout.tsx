import type { ReactNode } from "react";
import { Shell } from "@/components/Shell";

export default function GameLayout({ children }: { children: ReactNode }) { return <Shell>{children}</Shell>; }
