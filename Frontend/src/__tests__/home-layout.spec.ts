import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";

import type { HomeSettings } from "@/types/home";

import { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { placeHomeWidget } from "@/utils/home-grid";
import {
  defaultHomeLayout,
  selectedHomeLayout,
  setSeparateMobileLayout,
  updateHomeWidgetOptions,
} from "@/utils/home-layout";

const settings = (): HomeSettings => ({
  initialized: true,
  showWidgetTitles: false,
  showHeaderGreeting: true,
  useSeparateMobileLayout: false,
  desktop: defaultHomeLayout(),
  mobile: null,
});

describe("Home layout drafts", () => {
  beforeEach(() => setActivePinia(createPinia()));

  it("shares selection and order on mobile until a separate layout is enabled", () => {
    const value = settings();

    expect(selectedHomeLayout(value, "mobile")).toBe(value.desktop);
    setSeparateMobileLayout(value, true);
    expect(selectedHomeLayout(value, "mobile")).toBe(value.mobile);
    expect(value.mobile!.columns).toBe(4);
    expect(value.desktop.columns).toBe(12);
    expect(value.mobile!.widgets.map((widget) => widget.type)).toEqual(
      value.desktop.widgets.map((widget) => widget.type),
    );
    expect(
      new Set(
        [...value.desktop.widgets, ...value.mobile!.widgets].map((widget) => widget.instanceId),
      ).size,
    ).toBe(4);
    expect(value.mobile).not.toBe(value.desktop);
  });

  it("deep-copies the current desktop draft and keeps mobile options independent", () => {
    const value = settings();
    value.desktop.widgets[0]!.options.timeZone = "UTC";
    placeHomeWidget(value.desktop, { ...value.desktop.widgets[1]!, y: 0 });
    setSeparateMobileLayout(value, true);
    value.mobile!.widgets[1]!.options.timeZone = "Asia/Tokyo";
    value.mobile!.widgets[0]!.size = HomeWidgetSize.Small;
    value.mobile!.widgets.pop();

    expect(value.desktop.widgets.map((widget) => widget.type)).toEqual([
      HomeWidgetType.LibraryShortcuts,
      HomeWidgetType.Clock,
    ]);
    expect(value.desktop.widgets[1]!.options.timeZone).toBe("UTC");
    expect(value.desktop.widgets[0]!.size).toBe(HomeWidgetSize.Large);
  });

  it("disabling then re-enabling restores even an intentionally empty mobile layout", () => {
    const value = settings();
    setSeparateMobileLayout(value, true);
    value.mobile!.widgets = [];
    setSeparateMobileLayout(value, false);

    expect(selectedHomeLayout(value, "mobile")).toBe(value.desktop);
    expect(value.mobile!.widgets).toEqual([]);
    setSeparateMobileLayout(value, true);
    expect(selectedHomeLayout(value, "mobile").widgets).toEqual([]);
  });

  it("updates only the selected draft options while retaining widget references and geometry", () => {
    const value = settings();
    setSeparateMobileLayout(value, true);
    const layout = value.desktop;
    const widgets = [...layout.widgets];
    const options = widgets.map((widget) => widget.options);
    const mobile = structuredClone(value.mobile);
    const rectangles = widgets.map(({ x, y, width, height }) => ({ x, y, width, height }));

    expect(updateHomeWidgetOptions(layout, widgets[0]!.instanceId, { timeZone: "UTC" })).toBe(true);
    expect(updateHomeWidgetOptions(layout, "missing", { timeZone: "UTC" })).toBe(false);

    layout.widgets.forEach((widget, index) => expect(widget).toBe(widgets[index]));
    expect(layout.widgets[0]!.options).not.toBe(options[0]);
    expect(layout.widgets[1]!.options).toBe(options[1]);
    expect(layout.widgets.map(({ x, y, width, height }) => ({ x, y, width, height }))).toEqual(
      rectangles,
    );
    expect(value.mobile).toEqual(mobile);
  });

  it("edits a deep draft, leaves committed state alone and Cancel discards both drafts", () => {
    const store = useHomeStore();
    const value = settings();
    setSeparateMobileLayout(value, true);
    store.setOwner("alice");
    store.hydrate("alice", value);
    store.beginEditing("mobile");
    store.activeLayout!.widgets[0]!.options.timeZone = "UTC";
    store.draft!.desktop.widgets.pop();

    expect(store.settings).toEqual(value);
    expect(store.isDirty).toBe(true);
    store.cancelEditing();
    expect(store.draft).toBeNull();
    expect(store.settings).toEqual(value);
    expect(store.isDirty).toBe(false);
  });

  it("hydrates from the server response on commit, including empty layouts", () => {
    const store = useHomeStore();
    store.setOwner("alice");
    store.hydrate("alice", settings());
    store.beginEditing("desktop");
    const response = settings();
    response.desktop.widgets = [];
    store.commit("alice", response);
    response.desktop.widgets = defaultHomeLayout().widgets;

    expect(store.settings!.desktop.widgets).toEqual([]);
    expect(store.isEditing).toBe(false);
  });

  it("account changes clear drafts and reject late hydration from another owner", () => {
    const store = useHomeStore();
    store.setOwner("alice");
    store.hydrate("alice", settings());
    store.beginEditing("mobile");
    const previousEpoch = store.ownerEpoch;
    store.setOwner("bob");
    store.hydrate("alice", settings());
    store.commit("alice", settings());

    expect(store.ownerEpoch).toBeGreaterThan(previousEpoch);
    expect(store.settings).toBeNull();
    expect(store.draft).toBeNull();
    expect(store.target).toBe("desktop");
  });
});
