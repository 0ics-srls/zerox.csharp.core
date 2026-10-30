import { defineConfig } from "vitest/config";

// The tests are imported by Node itself, not by Vite's module runner: Vite reads a module's path as a URL
// and cuts it at a "#", so a checkout under a folder whose name has one (c#-…) found no test file. Node
// imports any path, and runs the TypeScript as it is (erasableSyntaxOnly in tsconfig.json).
export default defineConfig({
  test: { experimental: { viteModuleRunner: false } },
});
