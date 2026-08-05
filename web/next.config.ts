import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Emits a self-contained server bundle, which is what the runtime Docker stage
  // copies instead of the whole node_modules tree.
  output: "standalone",
};

export default nextConfig;
