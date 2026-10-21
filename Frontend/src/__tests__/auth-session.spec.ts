import { PiniaColada, defineQueryOptions, useQuery, useQueryCache } from "@pinia/colada";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { type Component, createApp, defineComponent, nextTick } from "vue";

import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { useSettingsStore } from "@/stores/settings";
import { useTabStore } from "@/stores/tab";

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

const mountWithColada = (component: Component) => {
  const pinia = createPinia();
  const app = createApp(component);

  app.use(pinia);
  app.use(PiniaColada);
  app.mount(document.createElement("div"));
  setActivePinia(pinia);
  mountedApps.push(app);

  return app;
};

beforeEach(() => {
  mockLogin.mockReset();
  mockLogout.mockReset();
  mockLogout.mockResolvedValue(undefined);
});

afterEach(() => {
  mountedApps.splice(0).forEach((app) => app.unmount());
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
});
