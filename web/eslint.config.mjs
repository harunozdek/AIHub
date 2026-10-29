import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

export default defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    rules: {
      // Log içeriği hiçbir zaman HTML olarak yorumlanmaz; dangerouslySetInnerHTML araçla yasak (Spec 0002, R-6).
      "react/no-danger": "error",
    },
  },
  globalIgnores([".next/**", "out/**", "next-env.d.ts", "playwright-report/**", "test-results/**"]),
]);
