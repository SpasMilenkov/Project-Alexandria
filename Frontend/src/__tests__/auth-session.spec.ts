import { PiniaColada, defineQueryOptions, useQuery, useQueryCache } from "@pinia/colada";
import { createPinia, setActivePinia } from "pinia";
import piniaPluginPersistedstate from "pinia-plugin-persistedstate";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { type Component, createApp, defineComponent, nextTick } from "vue";

import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { useSettingsStore } from "@/stores/settings";
import { useTabStore } from "@/stores/tab";
import {
  SESSION_EPOCH_STORAGE_KEY,
  stopSessionBoundaryWatcher,
} from "@/utils/sessionBoundary";

const { mockLogin, mockLogout } = vi.hoisted(() => ({
  mockLogin: vi.fn(),
  mockLogout: vi.fn(),
}));

vi.mock("@/api/auth", () => ({
  authApi: {
    login: mockLogin,
    logout: mockLogout,
  },
}));

const makeAuthResponse = (id = "user-b") => ({
  onboardingStep: 0,
  success: true,
  user: { email: `${id}@test.com`, id, name: id },
  userRoles: ["User"],
});

const mountedApps: ReturnType<typeof createApp>[] = [];

const mountWithColada = (component: Component, persist = false) => {
  const pinia = createPinia();

  if (persist) {
    pinia.use(piniaPluginPersistedstate);
  }

  const app = createApp(component);

  app.use(pinia);
  app.use(PiniaColada);
  app.mount(document.createElement("div"));
  setActivePinia(pinia);
  mountedApps.push(app);

  return app;
};

beforeEach(() => {
  stopSessionBoundaryWatcher();
  localStorage.clear();
  mockLogin.mockReset();
  mockLogout.mockReset();
  mockLogout.mockResolvedValue(undefined);
});

afterEach(() => {
  mountedApps.splice(0).forEach((app) => app.unmount());
  vi.restoreAllMocks();
});

describe("auth session teardown", () => {
  it("clears cached queries and owner-derived stores on clearSession", () => {
    mountWithColada(defineComponent({ setup: () => () => null }));

    const auth = useAuthStore();
    const cache = useQueryCache();
    const home = useHomeStore();
    const settings = useSettingsStore();
    const tabs = useTabStore();
    const initialAccent = settings.accentColor;

    cache.setQueryData(["settings", "appearance"], { accentColor: "red" });
    cache.setQueryData(["files", "file", "file-1"], { id: "file-1" });
    home.setOwner("user-a");
    settings.setAccentColor("red");
    tabs.createTab(null);

    auth.clearSession();

    expect(auth.user).toBeNull();
    expect(cache.getEntries()).toHaveLength(0);
    expect(home.ownerId).toBeNull();
    expect(settings.accentColor).toBe(initialAccent);
    expect(tabs.tabs).toHaveLength(0);
  });

  it("discards a query that resolves after the session ends", async () => {
    let resolveQuery: (value: string) => void = () => {};
    const pending = new Promise<string>((resolve) => {
      resolveQuery = resolve;
    });
    const options = defineQueryOptions({
      key: ["settings", "late"],
      query: () => pending,
    });
    let query: ReturnType<typeof useQuery> | null = null;

    mountWithColada(
      defineComponent({
        setup() {
          query = useQuery(options);

          return () => null;
        },
      }),
    );

    await nextTick();

    useAuthStore().clearSession();
    resolveQuery("late value");
    await nextTick();
    await nextTick();

    expect(useQueryCache().getEntries()).toHaveLength(0);
    expect(query?.state.value.status).toBe("pending");
  });

  it("clears the previous account's cache on login", async () => {
    mountWithColada(defineComponent({ setup: () => () => null }));

    const cache = useQueryCache();

    cache.setQueryData(["settings", "appearance"], { accentColor: "red" });
    mockLogin.mockResolvedValueOnce(makeAuthResponse());

    await useAuthStore().login({ email: "b@test.com", password: "pw" });

    expect(cache.getEntries()).toHaveLength(0);
    expect(useAuthStore().user?.user.id).toBe("user-b");
  });

  it("clears local state even when the logout request fails", async () => {
    mountWithColada(defineComponent({ setup: () => () => null }));

    const auth = useAuthStore();
    const cache = useQueryCache();
    const settings = useSettingsStore();
    const tabs = useTabStore();
    const initialAccent = settings.accentColor;

    mockLogin.mockResolvedValueOnce(makeAuthResponse());
    await auth.login({ email: "b@test.com", password: "pw" });

    cache.setQueryData(["settings", "appearance"], { accentColor: "red" });
    settings.setAccentColor("red");
    tabs.createTab(null);
    mockLogout.mockRejectedValueOnce(new Error("Network error"));

    await auth.logout();

    expect(auth.user).toBeNull();
    expect(cache.getEntries()).toHaveLength(0);
    expect(settings.accentColor).toBe(initialAccent);
    expect(tabs.tabs).toHaveLength(0);
  });

  it("rotates the shared session epoch on login and logout", async () => {
    mountWithColada(defineComponent({ setup: () => () => null }));

    const auth = useAuthStore();
    const initialEpoch = localStorage.getItem(SESSION_EPOCH_STORAGE_KEY);

    mockLogin.mockResolvedValueOnce(makeAuthResponse());
    await auth.login({ email: "b@test.com", password: "pw" });

    const loginEpoch = localStorage.getItem(SESSION_EPOCH_STORAGE_KEY);

    expect(loginEpoch).toBeTruthy();
    expect(loginEpoch).not.toBe(initialEpoch);

    await auth.logout();

    const logoutEpoch = localStorage.getItem(SESSION_EPOCH_STORAGE_KEY);

    expect(logoutEpoch).toBeTruthy();
    expect(logoutEpoch).not.toBe(loginEpoch);
  });

  it("persists the new auth state before rotating the session epoch", async () => {
    mountWithColada(defineComponent({ setup: () => () => null }), true);

    const auth = useAuthStore();
    const loginEpoch = "login-epoch";

    mockLogin.mockResolvedValueOnce(makeAuthResponse());
    localStorage.setItem(SESSION_EPOCH_STORAGE_KEY, loginEpoch);

    const setItem = vi.spyOn(Storage.prototype, "setItem");

    await auth.login({ email: "b@test.com", password: "pw" });

    const persistedAuth = JSON.parse(localStorage.getItem("auth") ?? "{}");
    const authWriteIndex = setItem.mock.calls.findIndex(([key]) => key === "auth");
    const playerWriteIndex = setItem.mock.calls.findIndex(([key]) => key === "player-v2");
    const tabWriteIndex = setItem.mock.calls.findIndex(([key]) => key === "tab");
    const epochWriteIndex = setItem.mock.calls.findIndex(
      ([key]) => key === SESSION_EPOCH_STORAGE_KEY,
    );

    expect(persistedAuth.user?.user?.id).toBe("user-b");
    expect(authWriteIndex).toBeGreaterThanOrEqual(0);
    expect(playerWriteIndex).toBeGreaterThanOrEqual(0);
    expect(tabWriteIndex).toBeGreaterThanOrEqual(0);
    expect(epochWriteIndex).toBeGreaterThanOrEqual(0);
    expect(authWriteIndex).toBeLessThan(epochWriteIndex);
    expect(playerWriteIndex).toBeLessThan(epochWriteIndex);
    expect(tabWriteIndex).toBeLessThan(epochWriteIndex);
  });

  it("does not rotate the session epoch for a failed login", async () => {
    mountWithColada(defineComponent({ setup: () => () => null }));

    const auth = useAuthStore();
    const initialEpoch = localStorage.getItem(SESSION_EPOCH_STORAGE_KEY);

    mockLogin.mockRejectedValueOnce(new Error("Invalid credentials"));

    await auth.login({ email: "bad@test.com", password: "wrong" });

    expect(localStorage.getItem(SESSION_EPOCH_STORAGE_KEY)).toBe(initialEpoch);
  });
});
