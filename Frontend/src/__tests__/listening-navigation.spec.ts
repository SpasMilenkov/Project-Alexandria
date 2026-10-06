import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createMemoryHistory, createRouter } from "vue-router";

import appRouter from "@/router";
import StatsHubView from "@/views/dashboard/streaming/wrapped/StatsHubView.vue";

const mounted = vi.hoisted(() => ({ timeline: vi.fn(), recaps: vi.fn() }));

vi.mock("@/stores/auth", () => ({
  useAuthStore: () => ({ user: { user: { id: "listener" } } }),
}));

vi.mock("@/components/streaming/wrapped/ListeningTimeline.vue", async () => {
  const { onMounted, ref } = await import("vue");

  return {
    default: {
      template: '<button @click="range++">Range {{ range }}</button>',

      setup: () => {
        onMounted(mounted.timeline);

        return { range: ref(0) };
      },
    },
  };
});

vi.mock("@/components/streaming/wrapped/ListeningRecaps.vue", async () => {
  const { onMounted } = await import("vue");

  return {
    default: {
      template: "<p>Recap collection</p>",

      setup: () => {
        onMounted(mounted.recaps);
      },
    },
  };
});

const createListeningRouter = () =>
  createRouter({
    history: createMemoryHistory(),

    routes: appRouter.options.routes
      .filter((route) => route.name === "stats-hub" || route.name === "timeline")
      .map((route) => {
        if (route.name === "stats-hub") return { ...route, component: StatsHubView };

        return route;
      }),
  });

afterEach(() => vi.clearAllMocks());

describe("Your listening navigation", () => {
  it("preserves the legacy timeline destination, query and hash", async () => {
    const router = createListeningRouter();

    await router.push("/stats/timeline?source=bookmark#calendar");
    expect(router.currentRoute.value.path).toBe("/stats");
    expect(router.currentRoute.value.query).toEqual({ source: "bookmark", view: "timeline" });
    expect(router.currentRoute.value.hash).toBe("#calendar");
  });

  it("keeps existing recap links and mounts only their section", async () => {
    const router = createListeningRouter();

    await router.push("/stats");

    const wrapper = mount(StatsHubView, { global: { plugins: [router] } });

    expect(wrapper.text()).toContain("Recap collection");
    expect(wrapper.findAll('a[aria-current="page"]')).toHaveLength(1);
    expect(wrapper.get('a[aria-current="page"]').text()).toBe("Recaps");
    expect(mounted.recaps).toHaveBeenCalledOnce();
    expect(mounted.timeline).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it("keeps the timeline range through section switches and browser Back", async () => {
    const router = createListeningRouter();

    await router.push("/stats?view=timeline&source=bookmark");

    const wrapper = mount(StatsHubView, { global: { plugins: [router] } });

    await wrapper.get("button").trigger("click");
    await wrapper.get('a[href*="view=recaps"]').trigger("click");
    await flushPromises();
    expect(router.currentRoute.value.query.source).toBe("bookmark");
    expect(wrapper.text()).toContain("Recap collection");
    router.back();
    await flushPromises();
    expect(wrapper.text()).toContain("Range 1");
    expect(wrapper.findAll('a[aria-current="page"]')).toHaveLength(1);
    expect(wrapper.get('a[aria-current="page"]').text()).toBe("Timeline");
    expect(mounted.timeline).toHaveBeenCalledOnce();
    wrapper.unmount();
  });
});
