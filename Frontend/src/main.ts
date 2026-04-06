import "./assets/main.css";
import ui from "@nuxt/ui/vue-plugin";
import { PiniaColada } from "@pinia/colada";
import { PiniaColadaRetry } from "@pinia/colada-plugin-retry";
import { createPinia } from "pinia";
import piniaPluginPersistedstate from "pinia-plugin-persistedstate";
import { createApp } from "vue";

import App from "./App.vue";
import { registerIcons } from "./icons";
import router from "./router";
import { logger } from "./utils/logger";
registerIcons();

const app = createApp(App);

const pinia = createPinia();
pinia.use(piniaPluginPersistedstate);

app.use(pinia);
app.use(PiniaColada, {
  mutationOptions: {},
  plugins: [
    PiniaColadaRetry({
      delay: (attempt) => Math.min(1000 * 2 ** attempt, 30_000),
      retry: 3,
    }),
  ],
  queryOptions: {
    staleTime: 30_000,
  },
});

app.use(router);
app.use(ui);

app.mount("#app");

if ("serviceWorker" in navigator) {
  const { getSerwist } = await import("virtual:serwist");
  const serwist = await getSerwist();
  serwist?.addEventListener("installed", () => {
    logger.log("Serwist installed!");
  });
  void serwist?.register();
}
