import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve } from "node:path";

export default defineConfig(({ command }) => {
  const isDevServer = command === "serve";

  return {
    plugins: [react()],
    base: isDevServer ? "/" : "./",
    build: {
      outDir: resolve(__dirname, "../wwwroot/app"),
      emptyOutDir: true,
      rollupOptions: {
        output: {
          entryFileNames: "assets/app-[hash].js",
          chunkFileNames: "assets/[name]-[hash].js",
          assetFileNames: (assetInfo) => {
            return assetInfo.names?.some((name) => name.endsWith(".css"))
              ? "assets/app-[hash].css"
              : "assets/[name]-[hash][extname]";
          }
        }
      }
    },
    server: {
      host: "127.0.0.1",
      port: 5173,
      strictPort: true
    }
  };
});
