import type { NextConfig } from "next";

// Trình duyệt gọi /api/* cùng origin; Next chuyển tiếp tới backend. Nhờ vậy không cần CORS ở dev và có thể chuyển sang cookie httpOnly sau này.
const config: NextConfig = {
  reactStrictMode: true,
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${process.env.API_URL ?? "http://127.0.0.1:5080"}/:path*` }];
  },
};
export default config;
