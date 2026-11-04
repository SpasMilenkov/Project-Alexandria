import { PiniaColada, useQueryCache } from "@pinia/colada";
import { createPinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent } from "vue";

import { directoryApi } from "@/api/directory";
import { HomeQuickAccessStyle, HomeQuickUploadStyle, HomeWidgetType } from "@/enums/home-widget";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";
import { homeFavoriteFolders } from "@/queries/home-widgets";
import {
  cloneHomeSettings,
  createHomeWidget,
  defaultHomeLayout,
  setSeparateMobileLayout,
} from "@/utils/home-layout";

afterEach(() => vi.restoreAllMocks());

describe("Home quick widget preferences and queries", () => {
  it("defaults existing saved widgets and keeps mobile style edits separate", () => {
    const existing = createHomeWidget(HomeWidgetType.QuickAccess, "folders");
    const document = cloneHomeSettings({
      initialized: true,
      showWidgetTitles: false,
      showHeaderGreeting: true,
      useSeparateMobileLayout: false,
      desktop: { ...defaultHomeLayout(), widgets: [existing] },
      mobile: null,
    });

    setSeparateMobileLayout(document, true);
    document.mobile!.widgets[0]!.options.quickAccessStyle = HomeQuickAccessStyle.Chips;

    expect(document.desktop.widgets[0]!.options.quickAccessStyle).toBe(HomeQuickAccessStyle.Tiles);
    expect(document.desktop.widgets[0]!.options.quickUploadStyle).toBe(
      HomeQuickUploadStyle.DropZone,
    );
    expect(document.mobile!.widgets[0]!.options.quickAccessStyle).toBe(HomeQuickAccessStyle.Chips);
  });

  it("requests a bounded live favorite page with owner and session isolation", async () => {
    const search = vi.spyOn(directoryApi, "searchDirectory").mockResolvedValue({
      currentPage: 0,
      items: [],
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
      hasNext: false,
      hasPrevious: false,
    });
    const app = createApp(defineComponent({ render: () => null }));
    app.use(createPinia());
    app.use(PiniaColada);
    const cache = app.runWithContext(() => useQueryCache());
    const options = homeFavoriteFolders({ userId: "alice", epoch: 1 });
    const entry = cache.ensure(options);

    await cache.fetch(entry);

    expect(search).toHaveBeenCalledWith({
      ownerId: "alice",
      isStarred: true,
      isDeleted: false,
      page: 0,
      pageSize: 20,
    });
    expect(entry.state.value.data).toEqual({
      userId: "alice",
      epoch: 1,
      value: expect.objectContaining({ items: [] }),
    });
    expect(options.key).not.toEqual(homeFavoriteFolders({ userId: "bob", epoch: 1 }).key);
    expect(options.key).not.toEqual(homeFavoriteFolders({ userId: "alice", epoch: 2 }).key);
    expect(homeFavoriteFolders({ userId: "", epoch: 1 }).enabled).toBe(false);

    const refresh = cache.getEntries({ key: [...DIRECTORY_QUERY_KEYS.root, "favorites"] });
    expect(refresh).toContain(entry);
    app.unmount();
  });
});
