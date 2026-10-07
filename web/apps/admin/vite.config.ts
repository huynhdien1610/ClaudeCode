import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// /admin/v1 được chuyển tiếp tới backend (API_URL, mặc định http://127.0.0.1:5080) — giống cách web người chơi dùng /api.
const target = process.env.API_URL ?? "http://127.0.0.1:5080";
const proxy = { "/admin/v1": { target, changeOrigin: true } };
export default defineConfig({ plugins: [react()], server: { proxy }, preview: { proxy } });
