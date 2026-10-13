import { afterEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, h, nextTick, ref } from "vue";

import { useHomeFocusTimer } from "@/composables/useHomeFocusTimer";
import {
  provideHomeWidgetPreview,
  useHomeWidgetQuery,
  useHomeWidgetThumbnail,
} from "@/composables/useHomeWidgetPreview";
import { HomeWidgetType } from "@/enums/home-widget";
import { createHomeWidgetPreview, homeWidgetShowcaseLayout } from "@/utils/home-widget-showcase";

const live = vi.hoisted(() => ({ query: vi.fn(), thumbnail: vi.fn() }));

vi.mock("@pinia/colada", () => ({ useQuery: live.query }));
vi.mock("@/composables/useFileThumbnail", () => ({ useFileThumbnail: live.thumbnail }));

const cleanups: (() => void)[] = [];

const mountPreview = <T>(setup: () => T): T => {
  const result = {} as { value: T };
  const child = defineComponent({
    setup: () => {
      result.value = setup();
      return () => null;
    },
  });
  const app = createApp(
    defineComponent({
      setup: () => {
        provideHomeWidgetPreview(createHomeWidgetPreview("demo-owner"));

        return () => h(child);
      },
    }),
  );

  app.mount(document.createElement("div"));
  cleanups.push(() => app.unmount());

  return result.value;
};

afterEach(() => {
  cleanups.splice(0).forEach((cleanup) => cleanup());
  vi.clearAllMocks();
});

describe("Home widget preview isolation", () => {
  it("uses reactive sample data without registering a live query or running its fetcher", async () => {
    const fetcher = vi.fn(() => Promise.resolve({ count: 0 }));
    const count = ref(1);
    const query = mountPreview(() =>
      useHomeWidgetQuery(
        () => ({ key: ["preview-test"], query: fetcher }),
        () => ({ count: count.value }),
      ),
    );

    expect(query.data.value).toEqual({ count: 1 });
    count.value = 2;
    await nextTick();
    await query.refresh();

    expect(query.data.value).toEqual({ count: 2 });
    expect(live.query).not.toHaveBeenCalled();
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("serves local artwork without initializing thumbnail retries", () => {
    mountPreview(() => {
      const thumbnail = useHomeWidgetThumbnail(() => ({ fileId: "demo", mimeType: "image/jpeg" }));

      expect(thumbnail.thumbnailUrl.value).toMatch(/^data:image\/svg\+xml,/u);
      expect(thumbnail.thumbnailLoaded.value).toBe(true);
      thumbnail.onThumbnailError();
    });

    expect(live.thumbnail).not.toHaveBeenCalled();
  });

  it("keeps focus controls and duration changes outside persistent timer state", async () => {
    const before = { ...localStorage };
    const widget = ref(homeWidgetShowcaseLayout(HomeWidgetType.FocusTimer).widgets[0]!);
    const timer = mountPreview(() => useHomeFocusTimer(() => widget.value));
    timer.toggle();
    timer.reset();
    timer.skipBreak();
    widget.value.options.focusMinutes = 40;
    await nextTick();

    expect(timer.remaining.value).toBe(2400);
    expect(timer.state.value.status).toBe("idle");
    expect({ ...localStorage }).toEqual(before);
  });
});
