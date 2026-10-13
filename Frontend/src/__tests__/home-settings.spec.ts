import { PiniaColada, useQueryCache } from "@pinia/colada";
import { PiniaColadaRetry } from "@pinia/colada-plugin-retry";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, nextTick } from "vue";

import type { AuthResponse } from "@/api/auth";
import type { HomeSettings } from "@/types/home";

import { useHomeSettings } from "@/composables/useHomeSettings";
import { OnboardingStep } from "@/enums";
import { resetHomeSettings } from "@/mutations/home";
import { HOME_QUERY_KEYS } from "@/queries/home";
import { HOME_STATUS_ACTIVITY_QUERY_KEYS } from "@/queries/home-status-activity";
import { HOME_TOP_SONGS_QUERY_KEYS } from "@/queries/home-top-songs";
import { HOME_WIDGET_QUERY_KEYS } from "@/queries/home-widgets";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { defaultHomeLayout, setSeparateMobileLayout } from "@/utils/home-layout";

const api = vi.hoisted(() => ({
  getHome: vi.fn(),
  getHomeTimeZones: vi.fn(),
  updateHome: vi.fn(),
  resetHome: vi.fn(),
}));
vi.mock("@/api/settings", () => ({ settingsApi: api }));
vi.mock("@/api/home", () => ({
  homeApi: { getBoard: api.getHome, saveChanges: api.updateHome, resetBoard: api.resetHome },
}));

const settings = (): HomeSettings => ({
  initialized: true,
  showWidgetTitles: false,
  showHeaderGreeting: true,
  useSeparateMobileLayout: false,
  desktop: defaultHomeLayout(),
  mobile: null,
});

const account = (id: string): AuthResponse => ({
  success: true,
  user: { id, name: id, email: `${id}@example.test` },
  userRoles: ["User"],
  onboardingStep: OnboardingStep.Done,
});

const mountSettings = (authenticated = true, globalRetries = false) => {
  const pinia = createPinia();
  const auth = useAuthStore(pinia);
  auth.user = authenticated ? account("alice") : null;
  const captured = {} as {
    home: ReturnType<typeof useHomeSettings>;
    cache: ReturnType<typeof useQueryCache>;
    reset: ReturnType<typeof resetHomeSettings>;
  };
  const app = createApp(
    defineComponent({
      setup() {
        captured.home = useHomeSettings();
        captured.cache = useQueryCache();
        captured.reset = resetHomeSettings();

        return () => null;
      },
    }),
  );
  app.use(pinia);
  app.use(PiniaColada, { plugins: globalRetries ? [PiniaColadaRetry({ retry: 3 })] : [] });
  app.mount(document.createElement("div"));

  return { app, auth, ...captured, store: useHomeStore(pinia) };
};

const cleanups: (() => void)[] = [];

beforeEach(() => {
  api.getHome.mockReset().mockResolvedValue(settings());
  api.getHomeTimeZones.mockReset().mockResolvedValue(["local", "UTC"]);
  api.updateHome.mockReset();
  api.resetHome.mockReset();
});

afterEach(() => cleanups.splice(0).forEach((cleanup) => cleanup()));

describe("Home settings lifecycle", () => {
  it("loads the board while the shared timezone catalogue is still pending", async () => {
    let finish = (_identifiers: string[]) => {};
    api.getHomeTimeZones.mockReturnValue(
      new Promise<string[]>((resolve) => {
        finish = resolve;
      }),
    );
    const { app, home, cache, store } = mountSettings();
    cleanups.push(() => app.unmount());

    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    expect(home.isLoading.value).toBe(false);
    expect(home.error.value).toBeNull();
    expect(api.getHomeTimeZones).toHaveBeenCalledTimes(1);
    expect(api.updateHome).not.toHaveBeenCalled();

    finish(["local", "UTC", "Asia/Kathmandu"]);
    await vi.waitFor(() => {
      const entry = cache.getEntries({ key: ["settings", "home-time-zones"] })[0];
      expect(entry?.state.value.data).toEqual(["local", "UTC", "Asia/Kathmandu"]);
    });
  });

  it("does not fetch the catalogue until authenticated", async () => {
    const { app, auth, store } = mountSettings(false);
    cleanups.push(() => app.unmount());
    await nextTick();

    expect(api.getHomeTimeZones).not.toHaveBeenCalled();
    expect(api.getHome).not.toHaveBeenCalled();

    auth.user = account("alice");
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    expect(api.getHomeTimeZones).toHaveBeenCalledTimes(1);
  });

  it("keeps catalogue failures separate from the board's loading and error states", async () => {
    api.getHomeTimeZones.mockRejectedValue(new Error("Timezone data unavailable"));
    const { app, home, cache, store } = mountSettings();
    cleanups.push(() => app.unmount());

    await vi.waitFor(() => {
      const entry = cache.getEntries({ key: ["settings", "home-time-zones"] })[0];
      expect(entry?.state.value.error).toEqual(new Error("Timezone data unavailable"));
    });
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    expect(home.isLoading.value).toBe(false);
    expect(home.error.value).toBeNull();
    expect(store.settings!.desktop.widgets).toHaveLength(2);
    expect(api.updateHome).not.toHaveBeenCalled();
  });

  it("does not write while editing; Done sends only changed fields and commits the response", async () => {
    const { app, home, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
    const clockId = store.draft!.desktop.widgets[0]!.instanceId;

    setSeparateMobileLayout(store.draft!, true);
    store.draft!.mobile!.widgets = [];
    store.draft!.desktop.widgets[0]!.options.timeZone = "UTC";
    await nextTick();

    expect(api.updateHome).not.toHaveBeenCalled();
    const response = settings();
    response.desktop.widgets = [];
    api.updateHome.mockResolvedValue(response);
    await home.save();

    expect(api.updateHome).toHaveBeenCalledWith(
      expect.objectContaining({
        preferences: { useSeparateMobileLayout: true },
        initializeMobileLayout: true,
        updates: [{ id: clockId, changes: { options: { timeZone: "UTC" } } }],
      }),
    );
    expect(store.settings).toEqual(response);
    expect(store.isEditing).toBe(false);
  });

  it("keeps a failed save draft for retry without changing committed settings", async () => {
    const { app, home, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
    store.draft!.desktop.widgets = [];
    api.updateHome.mockRejectedValueOnce(new Error("offline"));

    await expect(home.save()).rejects.toThrow("offline");
    expect(store.settings!.desktop.widgets).toHaveLength(2);
    expect(store.draft!.desktop.widgets).toEqual([]);
    expect(store.isDirty).toBe(true);
    api.updateHome.mockResolvedValue(settings());
    await home.save();
    expect(store.isEditing).toBe(false);
  });

  it("cancels and removes previous-account queries and ignores a late save", async () => {
    const { app, auth, cache, home, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
    let finish: (value: HomeSettings) => void = () => {
      throw new Error("Save has not started.");
    };
    api.updateHome.mockReturnValue(
      new Promise<HomeSettings>((resolve) => {
        finish = resolve;
      }),
    );
    const pending = home.save();
    await vi.waitFor(() => expect(api.updateHome).toHaveBeenCalledTimes(1));
    auth.user = account("bob");
    await vi.waitFor(() => expect(store.ownerId).toBe("bob"));
    expect(home.isSaving.value).toBe(false);
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    const late = settings();
    late.desktop.widgets = [];
    finish(late);
    await pending;

    expect(store.settings!.desktop.widgets).toHaveLength(2);
    expect(store.draft).toBeNull();
    expect(cache.getEntries({ key: HOME_QUERY_KEYS.settings("alice") })).toHaveLength(0);
  });

  it("removes private widget data from the previous account without clearing another owner", async () => {
    const { app, auth, cache, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    const storageKey = [...HOME_WIDGET_QUERY_KEYS.storageOwner("alice"), store.ownerEpoch];
    const createdKey = [...HOME_WIDGET_QUERY_KEYS.filesOwner("alice"), store.ownerEpoch, 0];
    const updatedKey = [...HOME_WIDGET_QUERY_KEYS.filesOwner("alice"), store.ownerEpoch, 1];
    const foldersKey = [...HOME_WIDGET_QUERY_KEYS.foldersOwner("alice"), store.ownerEpoch];
    const statusKey = [
      ...HOME_STATUS_ACTIVITY_QUERY_KEYS.statusOwner("alice"),
      store.ownerEpoch,
      false,
    ];
    const activityKey = [
      ...HOME_STATUS_ACTIVITY_QUERY_KEYS.activityOwner("alice"),
      store.ownerEpoch,
    ];
    const songsKey = [...HOME_TOP_SONGS_QUERY_KEYS.owner("alice"), store.ownerEpoch];
    const otherKey = [...HOME_WIDGET_QUERY_KEYS.filesOwner("bob"), 100, 0];

    for (const key of [
      storageKey,
      createdKey,
      updatedKey,
      foldersKey,
      statusKey,
      activityKey,
      songsKey,
      otherKey,
    ])
      cache.setQueryData(key, { private: true });

    auth.user = account("bob");
    await vi.waitFor(() => expect(store.ownerId).toBe("bob"));

    expect(cache.getEntries({ key: HOME_WIDGET_QUERY_KEYS.storageOwner("alice") })).toHaveLength(0);
    expect(cache.getEntries({ key: HOME_WIDGET_QUERY_KEYS.filesOwner("alice") })).toHaveLength(0);
    expect(cache.getEntries({ key: HOME_WIDGET_QUERY_KEYS.foldersOwner("alice") })).toHaveLength(0);
    expect(
      cache.getEntries({ key: HOME_STATUS_ACTIVITY_QUERY_KEYS.statusOwner("alice") }),
    ).toHaveLength(0);
    expect(
      cache.getEntries({ key: HOME_STATUS_ACTIVITY_QUERY_KEYS.activityOwner("alice") }),
    ).toHaveLength(0);
    expect(cache.getEntries({ key: HOME_TOP_SONGS_QUERY_KEYS.owner("alice") })).toHaveLength(0);
    expect(cache.getQueryData(otherKey)).toEqual({ private: true });
  });

  it("rejects a prior-session response after logging back into the same account", async () => {
    const { app, auth, home, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
    let finish: (value: HomeSettings) => void = () => {
      throw new Error("Save has not started.");
    };
    api.updateHome.mockReturnValue(
      new Promise<HomeSettings>((resolve) => {
        finish = resolve;
      }),
    );
    const pending = home.save();
    await vi.waitFor(() => expect(api.updateHome).toHaveBeenCalledTimes(1));
    auth.user = null;
    await nextTick();
    auth.user = account("alice");
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
    const late = settings();
    late.desktop.widgets = [];
    finish(late);
    await pending;

    expect(store.settings!.desktop.widgets).toHaveLength(2);
    expect(store.isEditing).toBe(true);
  });
});

describe("Home reset lifecycle", () => {
  it("resets after a failed read, clears the query error, and keeps the dashboard empty on refresh", async () => {
    api.getHome.mockRejectedValueOnce(new Error("invalid widget options"));
    const empty = settings();
    empty.desktop.widgets = [];
    api.resetHome.mockResolvedValue(empty);
    const { app, home, reset, store, cache } = mountSettings(true, true);
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(home.error.value).not.toBeNull());
    expect(store.settings).toBeNull();

    await reset.mutateAsync({ ownerId: "alice", epoch: store.ownerEpoch });
    await nextTick();

    expect(api.resetHome).toHaveBeenCalledTimes(1);
    expect(store.settings).toEqual(empty);
    expect(home.error.value).toBeNull();
    expect(cache.getQueryData(HOME_QUERY_KEYS.session("alice", store.ownerEpoch))).toEqual({
      ownerId: "alice",
      epoch: store.ownerEpoch,
      value: empty,
    });
    await home.refresh();
    expect(api.getHome).toHaveBeenCalledTimes(1);
    expect(store.settings!.desktop.widgets).toEqual([]);
  });

  it("keeps the existing board and draft when reset fails and allows retry", async () => {
    const { app, reset, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
    store.draft!.showWidgetTitles = true;
    api.resetHome.mockRejectedValueOnce(new Error("offline"));
    const request = { ownerId: "alice", epoch: store.ownerEpoch };

    await expect(reset.mutateAsync(request)).rejects.toThrow("offline");
    expect(store.settings!.desktop.widgets).toHaveLength(2);
    expect(store.draft!.showWidgetTitles).toBe(true);

    const empty = settings();
    empty.desktop.widgets = [];
    api.resetHome.mockResolvedValueOnce(empty);
    await reset.mutateAsync(request);

    expect(store.settings).toEqual(empty);
    expect(store.draft).toBeNull();
  });

  it("ignores a pending read that finishes after reset", async () => {
    let finishRead = (_value: HomeSettings) => {};
    api.getHome.mockReturnValue(
      new Promise<HomeSettings>((resolve) => {
        finishRead = resolve;
      }),
    );
    const empty = settings();
    empty.desktop.widgets = [];
    api.resetHome.mockResolvedValue(empty);
    const { app, reset, store, home } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(api.getHome).toHaveBeenCalledTimes(1));

    await reset.mutateAsync({ ownerId: "alice", epoch: store.ownerEpoch });
    finishRead(settings());
    await nextTick();
    await nextTick();

    expect(store.settings).toEqual(empty);
    expect(home.error.value).toBeNull();
  });

  it("ignores a reset response from a previous login session", async () => {
    const { app, auth, reset, store, cache } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    const priorEpoch = store.ownerEpoch;
    let finishReset = (_value: HomeSettings) => {};
    api.resetHome.mockReturnValue(
      new Promise<HomeSettings>((resolve) => {
        finishReset = resolve;
      }),
    );
    const pending = reset.mutateAsync({ ownerId: "alice", epoch: priorEpoch });
    await vi.waitFor(() => expect(api.resetHome).toHaveBeenCalledTimes(1));
    auth.user = null;
    await nextTick();
    auth.user = account("alice");
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    const empty = settings();
    empty.desktop.widgets = [];

    finishReset(empty);
    await pending;

    expect(store.settings!.desktop.widgets).toHaveLength(2);
    expect(cache.getEntries({ key: HOME_QUERY_KEYS.session("alice", priorEpoch) })).toHaveLength(0);
    await expect(reset.mutateAsync({ ownerId: "alice", epoch: priorEpoch })).rejects.toThrow(
      "account changed",
    );
    expect(api.resetHome).toHaveBeenCalledTimes(1);
  });
});
