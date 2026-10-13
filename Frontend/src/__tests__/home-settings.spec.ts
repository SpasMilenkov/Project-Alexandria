import { PiniaColada, useQueryCache } from "@pinia/colada";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, nextTick } from "vue";

import type { AuthResponse } from "@/api/auth";
import type { HomeSettings } from "@/types/home";

import { useHomeSettings } from "@/composables/useHomeSettings";
import { OnboardingStep } from "@/enums";
import { HOME_QUERY_KEYS } from "@/queries/home";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { defaultHomeLayout, setSeparateMobileLayout } from "@/utils/home-layout";

const api = vi.hoisted(() => ({
  getHome: vi.fn(),
  getHomeTimeZones: vi.fn(),
  updateHome: vi.fn(),
}));
vi.mock("@/api/settings", () => ({ settingsApi: api }));

const settings = (): HomeSettings => ({
  schemaVersion: 1,
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

const mountSettings = (authenticated = true) => {
  const pinia = createPinia();
  const auth = useAuthStore(pinia);
  auth.user = authenticated ? account("alice") : null;
  const captured = {} as {
    home: ReturnType<typeof useHomeSettings>;
    cache: ReturnType<typeof useQueryCache>;
  };
  const app = createApp(
    defineComponent({
      setup() {
        captured.home = useHomeSettings();
        captured.cache = useQueryCache();

        return () => null;
      },
    }),
  );
  app.use(pinia);
  app.use(PiniaColada);
  app.mount(document.createElement("div"));

  return { app, auth, ...captured, store: useHomeStore(pinia) };
};

describe("Home settings lifecycle", () => {
  const cleanups: (() => void)[] = [];

  beforeEach(() => {
    api.getHome.mockReset().mockResolvedValue(settings());
    api.getHomeTimeZones.mockReset().mockResolvedValue(["local", "UTC"]);
    api.updateHome.mockReset();
  });

  afterEach(() => cleanups.splice(0).forEach((cleanup) => cleanup()));

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
    expect(store.settings).toEqual(settings());
    expect(api.updateHome).not.toHaveBeenCalled();
  });

  it("does not write while editing; Done sends flag and both drafts and commits the response", async () => {
    const { app, home, store } = mountSettings();
    cleanups.push(() => app.unmount());
    await vi.waitFor(() => expect(store.settings).not.toBeNull());
    store.beginEditing("desktop");
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
        useSeparateMobileLayout: true,
        mobile: { columns: 4, widgets: [] },
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
