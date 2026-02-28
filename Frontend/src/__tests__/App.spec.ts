import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { describe, expect, it } from "vitest";
import { createMemoryHistory, createRouter } from "vue-router";

import App from "../App.vue";

const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ component: { template: "<div>You did it!</div>" }, path: "/" }],
});

describe("App", () => {
  it("mounts renders properly", async () => {
    const wrapper = mount(App, {
      global: {
        plugins: [createPinia(), router],
      },
    });

    await router.isReady();
    expect(wrapper.text()).toContain("You did it!");
  });
});
