import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { copyFileSync, existsSync, readdirSync } from "node:fs";
import { resolve } from "node:path";

function legacyAssetAliases(outDir) {
  return {
    name: "legacy-asset-aliases",
    closeBundle() {
      const assetsDir = resolve(outDir, "assets");
      if (!existsSync(assetsDir)) {
        return;
      }
      const files = readdirSync(assetsDir);
      const jsFile = files.find((name) => /^app-[\w-]+\.js$/.test(name));
      const cssFile = files.find((name) => /^app-[\w-]+\.css$/.test(name));

      if (jsFile) {
        copyFileSync(resolve(assetsDir, jsFile), resolve(assetsDir, "app.js"));
      }

      if (cssFile) {
        copyFileSync(resolve(assetsDir, cssFile), resolve(assetsDir, "app.css"));
      }
    }
  };
}

export default defineConfig(({ command }) => {
  const isDevServer = command === "serve";
  const outDir = resolve(__dirname, "../wwwroot/app");

  return {
    plugins: [react(), legacyAssetAliases(outDir)],
    base: isDevServer ? "/" : "./",
    build: {
      outDir,
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
