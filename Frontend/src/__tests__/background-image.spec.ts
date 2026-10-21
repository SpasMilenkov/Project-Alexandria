import { PiniaColada } from "@pinia/colada";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { type Component, createApp } from "vue";

import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import { useAuthStore } from "@/stores/auth";
import { useSettingsStore } from "@/stores/settings";
import { cancelBackgroundImageLoad } from "@/utils/backgroundImageObjectUrl";

const { mockDelete, mockGetUrl, mockUpload } = vi.hoisted(() => ({
  mockDelete: vi.fn(),
  mockGetUrl: vi.fn(),
  mockUpload: vi.fn(),
}));

vi.mock("@/api/settings", () => ({
  settingsApi: {
    deleteBackgroundImage: mockDelete,
    getAppearance: vi.fn(),
    getBackgroundImageUrl: mockGetUrl,
    getBehavior: vi.fn(),
    uploadBackgroundImage: mockUpload,
  },
}));

const mountedApps: ReturnType<typeof createApp>[] = [];
const createObjectURL = vi.fn();
const revokeObjectURL = vi.fn();
let objectUrlCount = 0;

const mountWithColada = (component: Component) => {
  const pinia = createPinia();
  const app = createApp(component);

  app.use(pinia);
  app.use(PiniaColada);
  app.mount(document.createElement("div"));
  setActivePinia(pinia);
  mountedApps.push(app);
};

const mountSession = () => {
  mountWithColada({ setup: () => () => null });

  const auth = useAuthStore();
  auth.user = {
    onboardingStep: 0,
    success: true,
    user: { email: "a@test.com", id: "user-a", name: "User A" },
    userRoles: ["User"],
  };

  return { auth, settings: useSettingsStore() };
};

const imageResponse = () =>
  new Response(new Blob(["image"], { type: "image/png" }), {
    headers: { "content-type": "image/png" },
    status: 200,
  });

const configureImage = (
  settings: ReturnType<typeof useSettingsStore>,
  key: string,
  version: string,
) => {
  settings.backgroundImageKey = key;
  settings.backgroundImageUpdatedAt = version;
};

beforeEach(() => {
  mockDelete.mockReset();
  mockGetUrl.mockReset();
  mockUpload.mockReset();
  createObjectURL.mockReset();
  revokeObjectURL.mockReset();
  objectUrlCount = 0;

  createObjectURL.mockImplementation(() => {
    objectUrlCount += 1;
    return `blob:image-${objectUrlCount}`;
  });

  Object.defineProperty(URL, "createObjectURL", {
    configurable: true,
    value: createObjectURL,
  });
  Object.defineProperty(URL, "revokeObjectURL", {
    configurable: true,
    value: revokeObjectURL,
  });

  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  cancelBackgroundImageLoad();
  mountedApps.splice(0).forEach((app) => app.unmount());
  vi.unstubAllGlobals();
});

describe("background image lifecycle", () => {
  it("loads an authorized image into a no-store blob URL", async () => {
    const { settings } = mountSession();
    const fetchMock = vi.mocked(fetch);
    const { syncBackgroundImage } = useBackgroundImageSync();

    configureImage(settings, "key-a", "v1");
    mockGetUrl.mockResolvedValue("/images/user-a?v=1");
    fetchMock.mockResolvedValue(imageResponse());

    await syncBackgroundImage("key-a", "v1");

    expect(fetchMock).toHaveBeenCalledWith(
      "/images/user-a?v=1",
      expect.objectContaining({
        cache: "no-store",
        credentials: "same-origin",
        signal: expect.any(AbortSignal),
      }),
    );
    expect(createObjectURL).toHaveBeenCalledTimes(1);
    expect(createObjectURL.mock.calls[0]?.[0]).toHaveProperty("type", "image/png");
    expect(settings.backgroundImage).toBe("blob:image-1");
  });

  it("revokes the previous blob when a new image replaces it", async () => {
    const { settings } = mountSession();
    const fetchMock = vi.mocked(fetch);
    const { syncBackgroundImage } = useBackgroundImageSync();

    configureImage(settings, "key-a", "v1");
    mockGetUrl.mockResolvedValue("/images/user-a?v=1");
    fetchMock.mockResolvedValueOnce(imageResponse());
    await syncBackgroundImage("key-a", "v1");

    configureImage(settings, "key-a", "v2");
    mockGetUrl.mockResolvedValue("/images/user-a?v=2");
    fetchMock.mockResolvedValueOnce(imageResponse());
    await syncBackgroundImage("key-a", "v2");

    expect(revokeObjectURL).toHaveBeenCalledWith("blob:image-1");
    expect(settings.backgroundImage).toBe("blob:image-2");
  });

  it("keeps the previous image when a replacement fetch fails", async () => {
    const { settings } = mountSession();
    const fetchMock = vi.mocked(fetch);
    const { syncBackgroundImage } = useBackgroundImageSync();

    configureImage(settings, "key-a", "v1");
    mockGetUrl.mockResolvedValue("/images/user-a?v=1");
    fetchMock.mockResolvedValueOnce(imageResponse());
    await syncBackgroundImage("key-a", "v1");

    configureImage(settings, "key-a", "v2");
    mockGetUrl.mockResolvedValue("/images/user-a?v=2");
    fetchMock.mockRejectedValueOnce(new Error("network down"));
    await syncBackgroundImage("key-a", "v2");

    expect(settings.backgroundImage).toBe("blob:image-1");
    expect(createObjectURL).toHaveBeenCalledTimes(1);
  });

  it("does not apply a delayed image response after deletion", async () => {
    const { settings } = mountSession();
    const { deleteBackgroundImage, syncBackgroundImage } = useBackgroundImageSync();

    let resolveFetch: (response: Response) => void = () => {};
    const pendingFetch = new Promise<Response>((resolve) => {
      resolveFetch = resolve;
    });

    configureImage(settings, "key-a", "v1");
    mockGetUrl.mockResolvedValue("/images/user-a?v=1");
    vi.mocked(fetch).mockReturnValue(pendingFetch);
    mockDelete.mockResolvedValue(undefined);

    const load = syncBackgroundImage("key-a", "v1");
    await Promise.resolve();
    await deleteBackgroundImage();
    resolveFetch(imageResponse());
    await load;

    expect(settings.backgroundImage).toBeNull();
    expect(settings.backgroundImageKey).toBeNull();
    expect(createObjectURL).not.toHaveBeenCalled();
  });

  it("cancels and revokes the blob on session teardown", async () => {
    const { auth, settings } = mountSession();
    const fetchMock = vi.mocked(fetch);
    const { syncBackgroundImage } = useBackgroundImageSync();

    configureImage(settings, "key-a", "v1");
    mockGetUrl.mockResolvedValue("/images/user-a?v=1");
    fetchMock.mockResolvedValue(imageResponse());
    await syncBackgroundImage("key-a", "v1");

    auth.clearSession();

    expect(revokeObjectURL).toHaveBeenCalledWith("blob:image-1");
    expect(settings.backgroundImage).toBeNull();
  });
});
