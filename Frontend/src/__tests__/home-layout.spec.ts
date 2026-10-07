import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";

import type { HomeSettings } from "@/types/home";

import { HomeWidgetSize } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import {
  defaultHomeLayout,
  moveHomeWidget,
  selectedHomeLayout,
  setSeparateMobileLayout,
} from "@/utils/home-layout";

const settings = (): HomeSettings => ({
  schemaVersion: 1,
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
    expect(value.mobile!.widgets.map((widget) => widget.instanceId)).toEqual(
      value.desktop.widgets.map((widget) => widget.instanceId),
    );
    expect(value.mobile).not.toBe(value.desktop);
  });

  it("deep-copies the current desktop draft and keeps mobile options independent", () => {
    const value = settings();
    value.desktop.widgets[0]!.options.timeZone = "UTC";
    moveHomeWidget(value.desktop, "default-library", 0);
    setSeparateMobileLayout(value, true);
    value.mobile!.widgets[1]!.options.timeZone = "Asia/Tokyo";
    value.mobile!.widgets[0]!.size = HomeWidgetSize.Small;
    value.mobile!.widgets.pop();

    expect(value.desktop.widgets.map((widget) => widget.instanceId)).toEqual([
      "default-library",
      "default-clock",
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

  it("reorders by instance identity without changing options and ignores invalid moves", () => {
    const layout = defaultHomeLayout();
    const clock = layout.widgets[0];
    moveHomeWidget(layout, "default-clock", 1);
    moveHomeWidget(layout, "missing", 0);
    moveHomeWidget(layout, "default-clock", -1);
    moveHomeWidget(layout, "default-clock", 2);

    expect(layout.widgets[1]!.instanceId).toBe(clock!.instanceId);
    expect(layout.widgets[1]!.options).toEqual(clock!.options);
    expect(layout.widgets.map((widget) => widget.instanceId)).toEqual([
      "default-library",
      "default-clock",
    ]);
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
