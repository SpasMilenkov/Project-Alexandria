import "./assets/main.css";
import ui from "@nuxt/ui/vue-plugin";
import { PiniaColada } from "@pinia/colada";
import { createPinia } from "pinia";
import piniaPluginPersistedstate from "pinia-plugin-persistedstate";
import { createApp } from "vue";

import App from "./App.vue";
import router from "./router";
import { logger } from "./utils/logger";

const app = createApp(App);

const pinia = createPinia();
pinia.use(piniaPluginPersistedstate);

app.use(pinia);
app.use(PiniaColada, {
  mutationOptions: {},
  plugins: [],
  queryOptions: {
    staleTime: 0,
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
