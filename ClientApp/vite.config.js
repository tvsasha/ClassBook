import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve } from "node:path";

export default defineConfig(({ command }) => {
  const isDevServer = command === "serve";

  return {
    plugins: [react()],
    base: isDevServer ? "/" : "/app/",
    build: {
      outDir: resolve(__dirname, "../wwwroot/app"),
      emptyOutDir: true
    },
    server: {
      host: "127.0.0.1",
      port: 5173,
      strictPort: true
    }
  };
});
