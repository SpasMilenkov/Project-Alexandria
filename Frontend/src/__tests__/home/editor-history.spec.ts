import { createPinia, setActivePinia } from "pinia";
import { describe, expect, it } from "vitest";
import { effectScope, ref } from "vue";

import { useHomeEditorHistory } from "@/composables/useHomeEditorHistory";
import { useHomeStore } from "@/stores/home";
import { cloneHomeSettings, defaultHomeLayout, setSeparateMobileLayout } from "@/utils/home-layout";
import { buildHomeChanges } from "@/utils/home-widget-persistence";

const setup = () => {
  setActivePinia(createPinia());

  const store = useHomeStore();
  store.setOwner("alice");
  store.hydrate("alice", {
    initialized: true,
    desktop: defaultHomeLayout(),
    mobile: null,
    useSeparateMobileLayout: false,
    showWidgetTitles: false,
    showHeaderGreeting: true,
  });
  store.beginEditing("desktop");

  const selectedId = ref<string | null>(store.draft!.desktop.widgets[0]!.instanceId);
  const scope = effectScope();
  const history = scope.run(() =>
    useHomeEditorHistory({
      session: () => `${store.ownerEpoch}:${store.isEditing}`,
      read: () => store.draft,
      write: (settings) => {
        store.draft = settings;
      },
      context: () => ({ target: store.target, selectedId: selectedId.value }),
      restoreContext: (context) => {
        store.target = context.target;
        selectedId.value = context.selectedId;
      },
    }),
  )!;

  return { store, history, selectedId, scope };
};

describe("Home editor history", () => {
  it("groups repeated keys, skips no-ops and branches after undo without mutating snapshots", () => {
    const { store, history, scope } = setup();
    const move = () => {
      store.draft!.desktop.widgets[0]!.x += 1;
    };

    history.record(() => {}, "right");
    expect(history.canUndo.value).toBe(false);

    history.record(move, "right");
    history.record(move, "right");
    history.record(move, "right");
    history.endGroup();
    history.record(move, "right");
    history.undo();
    expect(store.draft!.desktop.widgets[0]!.x).toBe(3);
    history.undo();
    expect(store.draft!.desktop.widgets[0]!.x).toBe(0);
    history.redo();
    expect(store.draft!.desktop.widgets[0]!.x).toBe(3);

    history.record(() => {
      store.draft!.desktop.widgets[0]!.options.timeZone = "UTC";
    });
    expect(history.canRedo.value).toBe(false);
    history.undo();
    expect(store.draft!.desktop.widgets[0]!.options.timeZone).toBe("local");
    history.redo();
    expect(store.draft!.desktop.widgets[0]!.options.timeZone).toBe("UTC");
    scope.stop();
  });

  it("restores both layouts, preferences and selection while preserving a stale editing baseline", () => {
    const { store, history, selectedId, scope } = setup();
    const baseline = cloneHomeSettings(store.baseline!);

    history.record(() => setSeparateMobileLayout(store.draft!, true));
    store.target = "mobile";
    selectedId.value = store.draft!.mobile!.widgets[0]!.instanceId;
    const mobileId = selectedId.value;
    history.record(() => {
      store.draft!.showWidgetTitles = true;
      store.draft!.mobile!.widgets = [];
      selectedId.value = null;
    });

    const remote = cloneHomeSettings(baseline);
    remote.desktop.widgets[0]!.options.timeZone = "Asia/Tokyo";
    store.hydrate("alice", remote);
    store.target = "desktop";
    history.undo();

    expect(store.target).toBe("mobile");
    expect(selectedId.value).toBe(mobileId);
    expect(store.draft!.mobile!.widgets).toHaveLength(2);
    expect(store.draft!.showWidgetTitles).toBe(false);
    expect(store.settings).toEqual(remote);
    expect(store.baseline).toEqual(baseline);

    history.undo();
    expect(store.draft).toEqual(baseline);
    expect(store.isDirty).toBe(false);
    expect(buildHomeChanges(store.baseline!, store.draft!).updates).toEqual([]);
    history.redo();
    expect(store.draft!.mobile!.widgets[0]!.instanceId).toBe(mobileId);
    scope.stop();
  });

  it("clears session history on cancel, successful save and account change", () => {
    const { store, history, scope } = setup();
    const edit = () =>
      history.record(() => {
        store.draft!.showWidgetTitles = true;
      });

    edit();
    store.cancelEditing();
    expect(history.canUndo.value).toBe(false);
    expect(history.canRedo.value).toBe(false);
    store.beginEditing("desktop");
    edit();
    store.commit("alice", cloneHomeSettings(store.draft!));
    expect(history.undo()).toBe(false);

    store.beginEditing("desktop");
    history.record(() => {
      store.draft!.showWidgetTitles = false;
    });
    store.setOwner("bob");
    expect(history.undo()).toBe(false);
    expect(store.draft).toBeNull();
    scope.stop();
  });
});
