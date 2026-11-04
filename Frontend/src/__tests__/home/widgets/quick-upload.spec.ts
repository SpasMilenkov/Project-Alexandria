import { PiniaColada, useQueryCache } from "@pinia/colada";
import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";

import HomeQuickUploadWidget from "@/components/dashboard/home/widgets/HomeQuickUploadWidget.vue";
import { HomeWidgetType } from "@/enums/home-widget";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { STATUS_QUERY_KEYS } from "@/queries/status";
import { useHomeStore } from "@/stores/home";
import { createHomeWidget, defaultHomeLayout } from "@/utils/home-layout";

const launcher = vi.hoisted(() => ({ open: vi.fn(), openDrop: vi.fn() }));

vi.mock("@/composables/useUploadLauncher", () => ({ useUploadLauncher: () => launcher }));
vi.mock("@/composables/useAppToast", () => ({ useAppToast: () => ({ error: vi.fn() }) }));

const Display = defineComponent({ name: "HomeQuickUploadDisplay", render: () => null });

const mountUpload = () => {
  const pinia = createPinia();
  const store = useHomeStore(pinia);
  store.setOwner("alice");
  store.hydrate("alice", {
    initialized: true,
    showWidgetTitles: false,
    showHeaderGreeting: true,
    useSeparateMobileLayout: false,
    desktop: defaultHomeLayout(),
    mobile: null,
  });

  const wrapper = mount(HomeQuickUploadWidget, {
    props: { widget: createHomeWidget(HomeWidgetType.QuickUpload, "upload") },
    global: { plugins: [pinia, PiniaColada], stubs: { HomeQuickUploadDisplay: Display } },
  });
  const cache = wrapper.vm.$.appContext.app.runWithContext(() => useQueryCache());
  const invalidate = vi.spyOn(cache, "invalidateQueries").mockImplementation(() => undefined);

  return { wrapper, store, invalidate, display: wrapper.findComponent(Display) };
};

beforeEach(() => {
  launcher.open.mockReset().mockResolvedValue(true);
  launcher.openDrop.mockReset().mockResolvedValue(false);
});

afterEach(() => vi.restoreAllMocks());

describe("Home quick upload lifecycle", () => {
  it("refreshes files, folder listings and storage after successful upload", async () => {
    const { wrapper, display, invalidate } = mountUpload();
    display.vm.$emit("upload", "File");

    await vi.waitFor(() => expect(invalidate).toHaveBeenCalledTimes(3));

    expect(launcher.open).toHaveBeenCalledWith("File");
    expect(invalidate).toHaveBeenCalledWith({ key: FILES_QUERY_KEYS.root });
    expect(invalidate).toHaveBeenCalledWith({ key: DIRECTORY_QUERY_KEYS.root });
    expect(invalidate).toHaveBeenCalledWith({ key: STATUS_QUERY_KEYS.myStorage() });
    wrapper.unmount();
  });

  it("suppresses uploads while arranging the board", async () => {
    const { wrapper, display, store } = mountUpload();
    store.beginEditing("desktop");
    display.vm.$emit("upload", "Directory");

    await wrapper.vm.$nextTick();

    expect(launcher.open).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it("blocks duplicate opens and refreshes after navigation away", async () => {
    let finish = (_value: boolean) => {};
    launcher.open.mockReturnValue(
      new Promise<boolean>((resolve) => {
        finish = resolve;
      }),
    );
    const { wrapper, display, invalidate } = mountUpload();
    display.vm.$emit("upload", "File");
    display.vm.$emit("upload", "Archive");

    expect(launcher.open).toHaveBeenCalledTimes(1);
    wrapper.unmount();
    finish(true);

    await vi.waitFor(() => expect(invalidate).toHaveBeenCalledTimes(3));
  });

  it("ignores upload completion from a previous account", async () => {
    let finish = (_value: boolean) => {};
    launcher.open.mockReturnValue(
      new Promise<boolean>((resolve) => {
        finish = resolve;
      }),
    );
    const { wrapper, display, invalidate, store } = mountUpload();
    display.vm.$emit("upload", "File");
    store.setOwner("bob");
    finish(true);

    await wrapper.vm.$nextTick();
    await Promise.resolve();

    expect(invalidate).not.toHaveBeenCalled();
    wrapper.unmount();
  });
});
