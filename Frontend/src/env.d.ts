/// <reference types="@serwist/vite/typings" />

declare const __ALEXANDRIA_API_BASE_URL__: string;

declare module "virtual:iconify-subset" {
  const subsets: Record<string, unknown>;
  export default subsets;
}
