import { PiniaColada } from "@pinia/colada";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createMemoryHistory, createRouter } from "vue-router";

import type { OverviewSummaryDtoResponse, OverviewSummaryHeaderDtoResponse } from "@/api/stats";

import AnnualRecapCard from "@/components/streaming/wrapped/AnnualRecapCard.vue";
import ListeningRecaps from "@/components/streaming/wrapped/ListeningRecaps.vue";
import RecapLandscape from "@/components/streaming/wrapped/RecapLandscape.vue";
import { annualRecapHeaders, annualRecapMonths } from "@/utils/listening-recaps.utils";

const api = vi.hoisted(() => ({
  getSummary: vi.fn(),
  listSummaries: vi.fn(),
  finalizeSummary: vi.fn(),
}));

vi.mock("@/api/stats", () => ({ statsApi: api }));

vi.mock("@/stores/auth", () => ({ useAuthStore: () => ({ user: { user: { id: "listener" } } }) }));

vi.mock("@/composables/useWrappedPalette", () => ({
  useWrappedPalette: () => ({ paletteStyle: {} }),
}));

const year = new Date().getUTCFullYear();

const header = (recapYear: number): OverviewSummaryHeaderDtoResponse => ({
  id: `recap-${recapYear}`,
  kind: 0,
  periodStart: `${recapYear}-01-01T00:00:00.000Z`,
  periodEnd: `${recapYear + 1}-01-01T00:00:00.000Z`,
  generatedAt: `${recapYear + 1}-01-02T00:00:00.000Z`,
  finalizedAt: `${recapYear + 1}-01-02T00:00:00.000Z`,
  isFinal: true,
  schemaVersion: 3,
});

const summary = (recapYear: number, seconds = 7200): OverviewSummaryDtoResponse => ({
  ...header(recapYear),
  isFinal: recapYear < year,
  periodEnd: recapYear === year ? new Date().toISOString() : header(recapYear).periodEnd,

  payload: {
    type: "wrapped",

    summary: {
      seconds,
      sessions: 83,
      qualifiedPlayCount: 61,
      tracks: 17,
      artists: 6,
      activeDays: 12,
      knownArtistShare: 1,
      hasPriorHistory: true,
    },

    deck: {
      cards: [
        {
          type: 2,
          headline: "Listening time",
          entries: [],

          visual: {
            colorKey: "blue",
            saturation: "",
            contrast: "",
            density: 0,
            fillLevel: 0,
            shapeGrammar: "landscape",
          },

          facts: {
            seconds,
            count: 12,
            share: 0,
            baselineSeconds: 0,
            weights: [],
            series: [{ date: `${recapYear}-01-01`, seconds, plays: 61 }],
          },
        },
      ],
    },

    visualIdentity: "recap",
    recipeVersion: 2,
    catalogVersion: "test",
  },
});

const observers: IntersectionObserverCallback[] = [];

class RecapObserver {
  constructor(callback: IntersectionObserverCallback) {
    observers.push(callback);
  }

  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
}

const wrappers: ReturnType<typeof mount>[] = [];

const mountRecaps = (component: typeof ListeningRecaps | typeof AnnualRecapCard, props = {}) => {
  const wrapper = mount(component, {
    props,

    global: {
      plugins: [
        createPinia(),
        PiniaColada,
        createRouter({
          history: createMemoryHistory(),
          routes: [{ path: "/:pathMatch(.*)*", component: { template: "<div />" } }],
        }),
      ],

      stubs: {
        UIcon: true,
        UBadge: { template: "<span><slot /></span>" },
        UButton: { props: ["to"], template: "<button><slot /></button>" },
      },
    },
  });

  wrappers.push(wrapper);

  return wrapper;
};

beforeEach(() => {
  vi.clearAllMocks();
  observers.length = 0;
  vi.stubGlobal("IntersectionObserver", RecapObserver);
  api.listSummaries.mockResolvedValue([]);

  api.getSummary.mockImplementation((_kind: string, from: string) =>
    Promise.resolve(summary(new Date(from).getUTCFullYear())),
  );
});

afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.unstubAllGlobals();
});

describe("annual recap overview", () => {
  it("selects only past complete calendar years, newest first", () => {
    const monthly = { ...header(year - 1), periodEnd: `${year - 1}-02-01T00:00:00Z` };

    expect(
      annualRecapHeaders([header(year - 2), header(year), monthly, header(year - 1)], year).map(
        (row) => row.id,
      ),
    ).toEqual([`recap-${year - 1}`, `recap-${year - 2}`]);
  });

  it("keeps month positions, zero months and exact supplied totals", () => {
    const months = annualRecapMonths(
      [
        { date: `${year}-09-01`, seconds: 3711, plays: 11 },
        { date: `${year - 1}-01-01`, seconds: 9999, plays: 9 },
      ],
      year,
    );

    expect(months).toHaveLength(12);
    expect(months[8]).toMatchObject({ seconds: 3711, plays: 11 });
    expect(months[0]).toMatchObject({ seconds: 0, plays: 0 });
    expect(months.reduce((total, month) => total + month.seconds, 0)).toBe(3711);
  });

  it("shows current listening even with no archived summaries", async () => {
    const wrapper = mountRecaps(ListeningRecaps);

    await flushPromises();
    expect(wrapper.text()).toContain(`${year} so far`);
    expect(wrapper.text()).toContain("No archived years yet");
    expect(wrapper.get("dl").text()).toContain("17");

    expect(api.getSummary).toHaveBeenCalledWith(
      "Wrapped",
      `${year}-01-01T00:00:00.000Z`,
      undefined,
    );
  });

  it("defers past payload requests until the card approaches the viewport", async () => {
    const wrapper = mountRecaps(AnnualRecapCard, { year: year - 1, header: header(year - 1) });

    await flushPromises();
    expect(api.getSummary).not.toHaveBeenCalled();

    observers[0]!(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      {} as IntersectionObserver,
    );

    await flushPromises();

    expect(api.getSummary).toHaveBeenCalledWith(
      "Wrapped",
      header(year - 1).periodStart,
      header(year - 1).periodEnd,
    );

    expect(wrapper.text()).toContain("61 plays");
    expect(wrapper.text()).not.toContain("83 plays");
    expect(wrapper.get("dl").text()).toContain("17");
    expect(wrapper.findComponent(RecapLandscape).props("series")[0].seconds).toBe(7200);
  });

  it("keeps this year's hero usable when the archive request fails", async () => {
    api.listSummaries.mockRejectedValue(new Error("Archive unavailable"));

    const wrapper = mountRecaps(ListeningRecaps);

    await flushPromises();
    expect(wrapper.text()).toContain("Could not load past recaps");
    expect(wrapper.get("dl").text()).toContain("17");
  });
});

describe("annual recap finalization and display", () => {
  it("finalizes an eligible past period and refreshes its frozen status", async () => {
    const pastYear = year - 1;

    api.getSummary
      .mockResolvedValueOnce({ ...summary(pastYear), isFinal: false })
      .mockResolvedValue(summary(pastYear));

    api.finalizeSummary.mockResolvedValue(summary(pastYear));

    const wrapper = mountRecaps(AnnualRecapCard, {
      year: pastYear,
      header: { ...header(pastYear), isFinal: false },
    });

    await flushPromises();

    observers[0]!(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      {} as IntersectionObserver,
    );

    await flushPromises();

    await wrapper
      .findAll("button")
      .find((button) => button.text() === "Finalize")!
      .trigger("click");

    await flushPromises();

    expect(api.finalizeSummary).toHaveBeenCalledWith(
      "Wrapped",
      header(pastYear).periodStart,
      header(pastYear).periodEnd,
    );

    expect(api.getSummary).toHaveBeenCalledTimes(2);
    expect(wrapper.findAll("button").some((button) => button.text() === "Finalize")).toBe(false);
    expect(wrapper.text()).toContain("Final");
  });

  it("uses a listening invitation when the current period is empty", async () => {
    api.getSummary.mockResolvedValue(summary(year, 0));

    const wrapper = mountRecaps(AnnualRecapCard, { year, current: true });

    await flushPromises();
    expect(wrapper.text()).toContain("Nothing to recap yet");
    expect(wrapper.text()).toContain("Find your soundtrack");
    expect(wrapper.find("dl").exists()).toBe(false);
  });

  it("does not label an unknown payload as empty listening", async () => {
    api.getSummary.mockResolvedValue({ ...summary(year), payload: { type: "new-format" } });

    const wrapper = mountRecaps(AnnualRecapCard, { year, current: true });

    await flushPromises();
    expect(wrapper.text()).toContain("This recap uses a newer format");
    expect(wrapper.text()).not.toContain("Nothing to recap yet");
  });

  it("supports keyboard month inspection and distinguishes future months", async () => {
    const wrapper = mount(RecapLandscape, {
      props: {
        year: 2026,
        through: "2026-10-05T00:00:00Z",
        series: [{ date: "2026-09-01", seconds: 7200, plays: 61 }],
      },
    });

    expect(wrapper.get("figcaption").text()).toContain("Busiest month: September");
    await wrapper.get('button[aria-label="November: still ahead"]').trigger("focus");
    expect(wrapper.get("figcaption").text()).toBe("November: still ahead");
    expect(wrapper.findAll('button[aria-pressed="true"]')).toHaveLength(1);
    wrapper.unmount();
  });
});
