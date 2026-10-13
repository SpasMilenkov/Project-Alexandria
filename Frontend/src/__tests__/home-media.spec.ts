import { PiniaColada } from "@pinia/colada";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, reactive, ref } from "vue";

import HomeCollectionsWidget from "@/components/dashboard/home/widgets/HomeCollectionsWidget.vue";
import HomeLatestRecapWidget from "@/components/dashboard/home/widgets/HomeLatestRecapWidget.vue";
import HomeTranscodingJobsWidget from "@/components/dashboard/home/widgets/HomeTranscodingJobsWidget.vue";
import { HomeRecapPeriod, HomeRecapStyle, HomeWidgetType } from "@/enums/home-widget";
import { JobStatus } from "@/enums/job-status";
import { homeMonthlyRecap } from "@/queries/home-media";
import { useHomeStore } from "@/stores/home";
import { createHomeWidget, defaultHomeLayout } from "@/utils/home-layout";
import { homeWidgetMinimum } from "@/utils/home-widget-sizing";

const api = vi.hoisted(() => ({
  jobs: vi.fn(),
  retry: vi.fn(),
  recap: vi.fn(),
  collections: vi.fn(),
  toast: vi.fn(),
}));

vi.mock("@/api/streaming", () => ({
  streamingApi: { getTranspilationJobs: api.jobs, updateTranspilationJob: api.retry },
}));
vi.mock("@/api/stats", () => ({ statsApi: { getWrapped: api.recap } }));
vi.mock("@/api/playlist", () => ({ playlistApi: { getAll: api.collections } }));
vi.mock("@/composables/useAppToast", () => ({ useAppToast: () => ({ error: api.toast }) }));
vi.mock("@/composables/useWrappedPalette", () => ({
  useWrappedPalette: () => ({ palette: ref<null>(null) }),
}));
vi.mock("@/composables/useHomeWidgetRows", () => ({ useHomeWidgetRows: () => ref(3) }));

const Button = defineComponent({
  props: { disabled: Boolean },
  setup:
    (props, { slots }) =>
    () =>
      h("button", { disabled: props.disabled }, slots.default?.()),
});

const Link = defineComponent({
  props: ["to"],
  setup:
    (props, { slots }) =>
    () =>
      h("a", { "data-to": JSON.stringify(props.to) }, slots.default?.()),
});

const paginated = (items: unknown[], totalCount = items.length) => ({ items, totalCount });

const recap = () => ({
  from: "2026-10-01T00:00:00Z",
  to: "2026-10-08T12:00:00Z",
  summary: { seconds: 7200, tracks: 8, artists: 3 },
  deck: {
    cards: [
      { type: 0, headline: "The artists you loved", entries: [{ title: "M83" }], visual: {} },
      { type: 1, headline: "Your songs", entries: [{ title: "Midnight City" }], visual: {} },
      { type: 7, headline: "You kept the music going", entries: [], visual: {} },
    ],
  },
});

const cleanups: (() => void)[] = [];

const mountWidgets = (types: HomeWidgetType[]) => {
  const pinia = createPinia();
  const home = useHomeStore(pinia);
  home.setOwner("alice");
  home.hydrate("alice", {
    initialized: true,
    showWidgetTitles: false,
    showHeaderGreeting: true,
    useSeparateMobileLayout: false,
    desktop: defaultHomeLayout(),
    mobile: null,
  });
  const widgets = reactive(types.map((type, index) => createHomeWidget(type, String(index))));
  const components = {
    [HomeWidgetType.TranscodingJobs]: HomeTranscodingJobsWidget,
    [HomeWidgetType.LatestRecap]: HomeLatestRecapWidget,
    [HomeWidgetType.Collections]: HomeCollectionsWidget,
  };
  const wrapper = mount(
    defineComponent({
      setup: () => () =>
        h(
          "div",
          widgets.map((widget) =>
            h(components[widget.type as keyof typeof components], { widget }),
          ),
        ),
    }),
    {
      global: {
        plugins: [pinia, PiniaColada],
        stubs: {
          Button,
          UButton: Button,
          UIcon: true,
          RouterLink: Link,
          WrappedArt: true,
          HomeCollectionCover: true,
        },
      },
    },
  );
  cleanups.push(() => wrapper.unmount());

  return { home, wrapper, widgets };
};

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-08T12:00:00Z"));

  vi.clearAllMocks();
  api.jobs.mockImplementation(({ status }: { status: JobStatus }) =>
    Promise.resolve(
      paginated(
        [
          {
            id: `job-${status}`,
            fileName: `Media ${status}`,
            status,
            retryCount: 1,
            isVideo: false,
          },
        ],
        100,
      ),
    ),
  );
  api.retry.mockResolvedValue(undefined);
  api.recap.mockResolvedValue(recap());
  api.collections.mockResolvedValue(paginated([{ id: "playlist", name: "Electronic" }], 45));
});

afterEach(() => {
  cleanups.splice(0).forEach((cleanup) => cleanup());
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Home media widgets", () => {
  it("shares bounded status requests across duplicate jobs widgets and retries a failed job", async () => {
    const { wrapper } = mountWidgets([
      HomeWidgetType.TranscodingJobs,
      HomeWidgetType.TranscodingJobs,
    ]);
    await flushPromises();

    expect(api.jobs).toHaveBeenCalledTimes(3);
    expect(api.jobs.mock.calls.map(([query]) => query.status)).toEqual([1, 0, 4]);
    api.jobs.mock.calls.forEach(([query]) =>
      expect(query).toMatchObject({ currentPage: 1, pageSize: 6 }),
    );

    await wrapper.get('[aria-label="Retry Media 4"]').trigger("click");
    await flushPromises();

    expect(api.retry).toHaveBeenCalledWith({ jobId: "job-4", status: JobStatus.Queued });
    expect(api.jobs.mock.calls.length).toBeGreaterThan(3);
    expect(wrapper.text()).not.toContain("%");
  });

  it("blocks retry during editing and gives actionable feedback on retry failure", async () => {
    const { wrapper, home } = mountWidgets([HomeWidgetType.TranscodingJobs]);
    await flushPromises();
    home.beginEditing();
    await flushPromises();
    await wrapper.get('[aria-label="Retry Media 4"]').trigger("click");
    expect(api.retry).not.toHaveBeenCalled();

    home.cancelEditing();
    api.retry.mockRejectedValueOnce(new Error("failed"));
    await flushPromises();
    await wrapper.get('[aria-label="Retry Media 4"]').trigger("click");
    await flushPromises();
    expect(api.toast).toHaveBeenCalledWith(
      "Could not retry this job",
      "Try again from your transcoding jobs.",
    );
  });

  it("uses one four-cover request and the complete collection count", async () => {
    const { wrapper } = mountWidgets([HomeWidgetType.Collections, HomeWidgetType.Collections]);
    await flushPromises();

    expect(api.collections).toHaveBeenCalledOnce();
    expect(api.collections).toHaveBeenCalledWith(1, 4, { isAutoGenerated: true });
    expect(
      wrapper.get('[aria-label="Browse 45 automatic collections"]').attributes("data-to"),
    ).toContain('"origin":"auto"');
  });

  it("fetches a current month once for all three recap styles", async () => {
    const { wrapper, widgets } = mountWidgets([
      HomeWidgetType.LatestRecap,
      HomeWidgetType.LatestRecap,
      HomeWidgetType.LatestRecap,
    ]);
    widgets[1]!.options.recapStyle = HomeRecapStyle.Numbers;
    widgets[2]!.options.recapStyle = HomeRecapStyle.Stories;
    await flushPromises();

    expect(api.recap).toHaveBeenCalledOnce();
    expect(api.recap).toHaveBeenCalledWith("2026-10-01T00:00:00.000Z", undefined);
    expect(wrapper.text()).toContain("So far");
    expect(wrapper.text()).toContain("Midnight City");
    expect(wrapper.text()).not.toContain("New artists");
    expect(wrapper.findAll('[role="group"][aria-label="Recap stories"] button')).toHaveLength(3);
  });

  it("requests a closed month and derives Final without a backend snapshot", async () => {
    const options = homeMonthlyRecap({ userId: "alice", epoch: 1, month: "2026-09" });
    await options.query({} as Parameters<typeof options.query>[0]);

    expect(api.recap).toHaveBeenCalledWith("2026-09-01T00:00:00.000Z", "2026-10-01T00:00:00.000Z");
    const { wrapper, widgets } = mountWidgets([HomeWidgetType.LatestRecap]);
    widgets[0]!.options.recapPeriod = HomeRecapPeriod.PreviousMonth;
    await flushPromises();
    expect(wrapper.text()).toContain("Final");
    expect(wrapper.get('[aria-label="Open September 2026 recap"]').attributes("data-to")).toContain(
      "2026-09",
    );
  });

  it("shows empty states when the owner has no jobs, stories or collections", async () => {
    api.jobs.mockResolvedValue(paginated([]));
    api.recap.mockResolvedValue({ ...recap(), deck: { cards: [] } });
    api.collections.mockResolvedValue(paginated([]));
    const { wrapper } = mountWidgets([
      HomeWidgetType.TranscodingJobs,
      HomeWidgetType.LatestRecap,
      HomeWidgetType.Collections,
    ]);
    await flushPromises();

    expect(wrapper.text()).toContain("Nothing to transcode");
    expect(wrapper.text()).toContain("No recap for October 2026");
    expect(wrapper.text()).toContain("No collections yet");
  });

  it("drops the old owner's content immediately while replacement queries wait", async () => {
    const { wrapper, home } = mountWidgets([
      HomeWidgetType.TranscodingJobs,
      HomeWidgetType.LatestRecap,
      HomeWidgetType.Collections,
    ]);
    await flushPromises();
    api.jobs.mockReturnValue(new Promise(() => {}));
    api.recap.mockReturnValue(new Promise(() => {}));
    api.collections.mockReturnValue(new Promise(() => {}));
    home.setOwner("bob");
    await flushPromises();

    expect(wrapper.text()).not.toContain("Media 1");
    expect(wrapper.text()).not.toContain("October 2026");
    expect(wrapper.text()).not.toContain("45");
  });

  it.each([HomeWidgetType.TranscodingJobs, HomeWidgetType.LatestRecap, HomeWidgetType.Collections])(
    "keeps type %s available at two by two on desktop and mobile",
    (type) => {
      expect(homeWidgetMinimum(type, 12)).toEqual({ width: 2, height: 2 });
      expect(homeWidgetMinimum(type, 4)).toEqual({ width: 2, height: 2 });
    },
  );
});
