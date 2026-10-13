import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";

import { HomeActivityStyle, HomeStatusStyle, HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { createHomeWidget, setSeparateMobileLayout } from "@/utils/home-layout";
import { homeWidgetMinimum } from "@/utils/home-widget-sizing";

describe("Status, song and activity composition", () => {
  beforeEach(() => setActivePinia(createPinia()));

  it("keeps mobile styles independent and discards a draft without changing committed preferences", () => {
    const status = createHomeWidget(HomeWidgetType.SystemStatus, "status");
    const activity = createHomeWidget(HomeWidgetType.RecentActivity, "activity");
    activity.y = 4;
    const value = {
      initialized: true,
      showWidgetTitles: false,
      showHeaderGreeting: true,
      useSeparateMobileLayout: false,
      desktop: { columns: 12 as const, widgets: [status, activity] },
      mobile: null,
    };

    setSeparateMobileLayout(value, true);
    const home = useHomeStore();
    home.setOwner("alice");
    home.hydrate("alice", value);
    home.beginEditing("mobile");
    home.activeLayout!.widgets[0]!.options.statusStyle = HomeStatusStyle.History;
    home.activeLayout!.widgets[1]!.options.activityStyle = HomeActivityStyle.ByDay;

    expect(home.draft!.desktop.widgets[0]!.options.statusStyle).toBe(HomeStatusStyle.Beacon);
    expect(home.draft!.desktop.widgets[1]!.options.activityStyle).toBe(HomeActivityStyle.Feed);
    expect(home.settings).toEqual(value);

    home.cancelEditing();

    expect(home.settings).toEqual(value);
    expect(home.isDirty).toBe(false);
  });

  it.each([HomeWidgetType.SystemStatus, HomeWidgetType.TopSongs, HomeWidgetType.RecentActivity])(
    "allows type %s to fit the compact two by two grid footprint on both devices",
    (type) => {
      expect(homeWidgetMinimum(type, 12)).toEqual({ width: 2, height: 2 });
      expect(homeWidgetMinimum(type, 4)).toEqual({ width: 2, height: 2 });
    },
  );
});
