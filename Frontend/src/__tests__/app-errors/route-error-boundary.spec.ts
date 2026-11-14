import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";

import RouteErrorBoundary from "@/components/common/RouteErrorBoundary.vue";

const stubs = {
  UButton: { emits: ["click"], template: "<button @click=\"$emit('click')\"><slot /></button>" },
  UIcon: true,
};

const createTestRouter = async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { component: { template: "<div />" }, path: "/" },
      { component: { template: "<div />" }, path: "/other" },
    ],
  });

  await router.push("/");
  await router.isReady();

  return router;
};

const mountBoundary = async (child: ReturnType<typeof defineComponent>) => {
  const router = await createTestRouter();
  const errorHandler = vi.fn();

  const wrapper = mount(
    defineComponent({
      setup: () => () =>
        h(RouteErrorBoundary, null, {
          default: ({ attempt }: { attempt: number }) => h(child, { key: attempt }),
        }),
    }),
    { global: { config: { errorHandler }, plugins: [createPinia(), router], stubs } },
  );

  return { errorHandler, router, wrapper };
};

describe("RouteErrorBoundary", () => {
  it("replaces a page whose setup throws with the error screen", async () => {
    const broken = defineComponent({
      setup: () => {
        throw new Error("boom");
      },
    });

    const { errorHandler, wrapper } = await mountBoundary(broken);

    const alert = wrapper.find('[role="alert"]');

    expect(alert.text()).toContain("This page failed to load");
    expect(alert.text()).toContain("boom");
    expect(errorHandler).not.toHaveBeenCalled();
  });

  it("remounts the page when the user tries again", async () => {
    let calls = 0;
    const flaky = defineComponent({
      setup: () => {
        calls += 1;
        if (calls === 1) throw new Error("first load failed");

        return () => h("p", "Recovered page");
      },
    });

    const { wrapper } = await mountBoundary(flaky);
    const tryAgain = wrapper.findAll("button").find((button) => button.text() === "Try again");
    await tryAgain?.trigger("click");

    expect(wrapper.text()).toContain("Recovered page");
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
  });

  it("leaves the page alone when only an event handler fails", async () => {
    const clicky = defineComponent({
      setup: () => () =>
        h(
          "button",
          {
            onClick: () => {
              throw new Error("handler failed");
            },
          },
          "Click me",
        ),
    });

    const { errorHandler, wrapper } = await mountBoundary(clicky);
    await wrapper.find("button").trigger("click");

    expect(wrapper.text()).toContain("Click me");
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
    expect(errorHandler).toHaveBeenCalledOnce();
  });

  it("offers a reload when the page files belong to an older build", async () => {
    const stale = defineComponent({
      setup: () => {
        throw new Error("Failed to fetch dynamically imported module: /assets/HomeView-abc.js");
      },
    });

    const { wrapper } = await mountBoundary(stale);

    expect(wrapper.find('[role="alert"]').text()).toContain("Alexandria was updated");
  });

  it("clears the error once the user navigates elsewhere", async () => {
    let calls = 0;
    const brokenOnce = defineComponent({
      setup: () => {
        calls += 1;
        if (calls === 1) throw new Error("boom");

        return () => h("p", "Next page");
      },
    });

    const { router, wrapper } = await mountBoundary(brokenOnce);
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);

    await router.push("/other");
    await flushPromises();

    expect(wrapper.text()).toContain("Next page");
  });
});
