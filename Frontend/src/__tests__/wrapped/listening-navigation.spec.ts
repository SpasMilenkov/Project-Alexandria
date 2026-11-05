import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createMemoryHistory, createRouter } from "vue-router";

import appRouter from "@/router";
import StatsHubView from "@/views/dashboard/streaming/wrapped/StatsHubView.vue";

const mounted = vi.hoisted(() => ({ timeline: vi.fn(), recaps: vi.fn() }));
const scroll = vi.fn();
const originalScroll = Object.getOwnPropertyDescriptor(HTMLElement.prototype, "scrollIntoView");

vi.mock("@/stores/auth", () => ({
  useAuthStore: () => ({ user: { user: { id: "listener" } } }),
}));

vi.mock("@/components/streaming/wrapped/listening/ListeningTimeline.vue", async () => {
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

const history = vi.hoisted(() => ({ empty: false }));

vi.mock("@/composables/useListeningHistoryEmpty", async () => {
  const { computed } = await import("vue");

  return { useListeningHistoryEmpty: () => ({ listeningEmpty: computed(() => history.empty) }) };
});

vi.mock("@/components/streaming/wrapped/listening/ListeningWelcomeHero.vue", () => ({
  default: { template: "<section><h1>Your listening</h1><h2>Welcome</h2></section>" },
}));

vi.mock("@/components/streaming/wrapped/listening/ListeningPreviews.vue", () => ({
  default: { template: "<section>What you will find here</section>" },
}));

vi.mock("@/components/streaming/wrapped/listening/ListeningHero.vue", () => ({
  default: { template: "<section><h1>Your listening</h1></section>" },
}));

vi.mock("@/components/streaming/wrapped/recaps/ListeningRecaps.vue", async () => {
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

beforeEach(() => {
  Object.defineProperty(HTMLElement.prototype, "scrollIntoView", {
    configurable: true,
    value: scroll,
  });
});

afterEach(() => {
  vi.clearAllMocks();
  history.empty = false;

  if (originalScroll)
    Object.defineProperty(HTMLElement.prototype, "scrollIntoView", originalScroll);
  else Reflect.deleteProperty(HTMLElement.prototype, "scrollIntoView");
});

describe("Your listening navigation", () => {
  it("preserves the legacy timeline destination, query and hash", async () => {
    const router = createListeningRouter();

    await router.push("/stats/timeline?source=bookmark#calendar");
    expect(router.currentRoute.value.path).toBe("/stats");
    expect(router.currentRoute.value.query).toEqual({ source: "bookmark", view: "timeline" });
    expect(router.currentRoute.value.hash).toBe("#calendar");
  });

  it("shows recaps followed by the timeline without section tabs", async () => {
    const router = createListeningRouter();

    await router.push("/stats");

    const wrapper = mount(StatsHubView, { global: { plugins: [router] } });

    expect(wrapper.text()).toContain("Recap collection");
    expect(wrapper.text()).toContain("Range 0");
    expect(wrapper.text().indexOf("Recap collection")).toBeLessThan(
      wrapper.text().indexOf("Range 0"),
    );
    expect(wrapper.find('[aria-label="Listening sections"]').exists()).toBe(false);
    expect(mounted.recaps).toHaveBeenCalledOnce();
    expect(mounted.timeline).toHaveBeenCalledOnce();
    wrapper.unmount();
  });

  it("replaces every section with one welcome when nothing has been played", async () => {
    history.empty = true;
    const router = createListeningRouter();

    await router.push("/stats");

    const wrapper = mount(StatsHubView, { global: { plugins: [router] } });

    expect(wrapper.text()).toContain("Welcome");
    expect(wrapper.text()).toContain("What you will find here");
    expect(wrapper.text()).not.toContain("Recap collection");
    expect(mounted.recaps).not.toHaveBeenCalled();
    expect(mounted.timeline).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it("keeps the timeline range through legacy query changes and browser Back", async () => {
    const router = createListeningRouter();

    await router.push("/stats?view=timeline&source=bookmark");

    const wrapper = mount(StatsHubView, { global: { plugins: [router] } });

    await flushPromises();
    expect(scroll).toHaveBeenCalledWith({ block: "start" });
    await wrapper.get("button").trigger("click");
    await router.push("/stats?view=recaps&source=bookmark");
    await flushPromises();
    expect(router.currentRoute.value.query.source).toBe("bookmark");
    expect(wrapper.text()).toContain("Recap collection");
    router.back();
    await flushPromises();
    expect(wrapper.text()).toContain("Range 1");
    expect(mounted.timeline).toHaveBeenCalledOnce();
    expect(mounted.recaps).toHaveBeenCalledOnce();
    wrapper.unmount();
  });

  it("jumps to the timeline anchor while keeping both sections mounted", async () => {
    const router = createListeningRouter();

    await router.push("/stats?source=bookmark");
    const wrapper = mount(StatsHubView, { global: { plugins: [router] } });

    await router.push({ query: router.currentRoute.value.query, hash: "#timeline" });
    await flushPromises();

    expect(router.currentRoute.value.hash).toBe("#timeline");
    expect(router.currentRoute.value.query.source).toBe("bookmark");
    expect(scroll).toHaveBeenCalledWith({ block: "start" });
    expect(mounted.timeline).toHaveBeenCalledOnce();
    expect(mounted.recaps).toHaveBeenCalledOnce();
    wrapper.unmount();
  });
});
