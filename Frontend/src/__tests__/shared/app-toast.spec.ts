import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { type App, createApp, defineComponent, isVNode } from "vue";

import { ApiError } from "@/api/errors";
import { type AppToast, useAppToast } from "@/composables/useAppToast";
import { TOAST_SOURCES, type ToastSource } from "@/features/toast/sources";
import { useSettingsStore } from "@/stores/settings";

interface Captured {
  appToast?: AppToast;
  toaster?: ReturnType<typeof useToast>;
}

const mountedApps: App[] = [];

const mountToast = (source?: ToastSource) => {
  const pinia = createPinia();
  setActivePinia(pinia);

  const captured: Captured = {};
  const app = createApp(
    defineComponent({
      setup() {
        captured.appToast = useAppToast(source);
        captured.toaster = useToast();
        return () => null;
      },
    }),
  );

  app.use(pinia);
  app.mount(document.createElement("div"));
  mountedApps.push(app);

  const { appToast, toaster } = captured;
  if (!appToast || !toaster) throw new Error("Toast composables did not initialize");

  toaster.clear();

  return { appToast, settings: useSettingsStore(), toaster };
};

// Titles render as a source label stacked over the message, so tests read the text back out.
const titleText = (title: unknown): string => {
  const node = typeof title === "function" ? title() : title;
  if (!isVNode(node)) return String(node);

  const children = node.children as unknown[];

  return children
    .map((child) => (isVNode(child) ? String(child.children) : String(child)))
    .join(" | ");
};

describe("useAppToast", () => {
  afterEach(() => {
    mountedApps.splice(0).forEach((app) => app.unmount());
  });

  describe("rendering", () => {
    it("labels every toast with the area that raised it and its icon", async () => {
      const mounted = mountToast("playlists");

      mounted.appToast.success('Created "Road trip"');
      await flushPromises();

      const [toast] = mounted.toaster.toasts.value;

      expect(titleText(toast?.title)).toBe(
        `${TOAST_SOURCES.playlists.label} | Created "Road trip"`,
      );
      expect(toast?.avatar).toMatchObject({
        color: "success",
        icon: TOAST_SOURCES.playlists.icon,
      });
      expect(toast?.color).toBe("success");
    });

    it("lets a single call override the default source", async () => {
      const mounted = mountToast("playlists");

      mounted.appToast.info("Copied 2 files", { source: "clipboard" });
      await flushPromises();

      const [toast] = mounted.toaster.toasts.value;

      expect(titleText(toast?.title)).toContain(TOAST_SOURCES.clipboard.label);
      expect(toast?.avatar?.icon).toBe(TOAST_SOURCES.clipboard.icon);
    });

    it("collapses an identical repeat into the existing toast instead of stacking", async () => {
      const mounted = mountToast("playlists");

      mounted.appToast.error("Couldn't save");
      mounted.appToast.error("Couldn't save");
      await flushPromises();

      expect(mounted.toaster.toasts.value).toHaveLength(1);
    });

    it("gives actions a neutral outline look unless the caller styles them", async () => {
      const mounted = mountToast("playlists");

      mounted.appToast.success("Moved 1 file", { actions: [{ label: "Undo" }] });
      await flushPromises();

      const [toast] = mounted.toaster.toasts.value;

      expect(toast?.actions).toEqual([{ color: "neutral", label: "Undo", variant: "outline" }]);
    });
  });

  describe("errors", () => {
    it("turns a thrown HTTP error into a readable cause line", async () => {
      const mounted = mountToast("files");

      mounted.appToast.error(
        "Couldn't rename",
        new ApiError("Request failed", { data: {}, kind: "http", status: 403 }),
      );
      await flushPromises();

      expect(mounted.toaster.toasts.value[0]?.description).toBe(
        "You don't have permission to do this.",
      );
    });

    it("offers Retry for transient failures and runs the callback", async () => {
      const mounted = mountToast("files");

      const retry = vi.fn();

      mounted.appToast.error(
        "Couldn't load",
        new ApiError("Request failed", { data: {}, kind: "http", status: 503 }),
        { retry },
      );
      await flushPromises();

      const retryAction = mounted.toaster.toasts.value[0]?.actions?.find(
        (action) => action.label === "Retry",
      );
      const onClick = retryAction?.onClick;

      expect(typeof onClick).toBe("function");
      if (typeof onClick === "function") onClick(new MouseEvent("click"));

      expect(retry).toHaveBeenCalledOnce();
    });

    it("withholds Retry when repeating the request cannot succeed", async () => {
      const mounted = mountToast("files");

      mounted.appToast.error(
        "Couldn't create",
        new ApiError("Request failed", {
          data: { errors: { name: ["Name is taken"] } },
          kind: "http",
          status: 409,
        }),
        { retry: vi.fn() },
      );
      await flushPromises();

      const [toast] = mounted.toaster.toasts.value;

      expect(toast?.description).toBe("Name is taken");
      expect(toast?.actions).toEqual([]);
    });
  });

  describe("toast level", () => {
    it("keeps errors and warnings but drops confirmations on errors-only", async () => {
      const mounted = mountToast();

      mounted.settings.toastLevel = "errors-only";

      mounted.appToast.success("Saved");
      mounted.appToast.info("Copied");
      mounted.appToast.warning("Partly saved");
      mounted.appToast.error("Couldn't save");
      await flushPromises();

      expect(mounted.toaster.toasts.value.map((toast) => toast.color)).toEqual([
        "warning",
        "error",
      ]);
    });

    it("shows nothing at all when silent", async () => {
      const mounted = mountToast();

      mounted.settings.toastLevel = "silent";

      mounted.appToast.error("Couldn't save");
      mounted.appToast.track("Uploading").fail("Couldn't upload");
      await flushPromises();

      expect(mounted.toaster.toasts.value).toHaveLength(0);
    });
  });

  describe("tracked operations", () => {
    it("shows a persistent pending toast that settles in place", async () => {
      const mounted = mountToast("metadata");

      const progress = mounted.appToast.track("Updating 3 tracks…");
      await flushPromises();

      const [pending] = mounted.toaster.toasts.value;

      expect(pending?.duration).toBe(0);
      expect(pending?.color).toBe("neutral");

      progress.succeed("Updated 3 tracks");
      await flushPromises();

      expect(mounted.toaster.toasts.value).toHaveLength(1);

      const [settled] = mounted.toaster.toasts.value;

      expect(settled?.id).toBe(pending?.id);
      expect(settled?.color).toBe("success");
      expect(titleText(settled?.title)).toContain("Updated 3 tracks");
      expect(settled?.duration).toBeGreaterThan(0);
    });

    it("surfaces a failure even when progress was hidden by errors-only", async () => {
      const mounted = mountToast("metadata");

      mounted.settings.toastLevel = "errors-only";

      const progress = mounted.appToast.track("Updating 3 tracks…");
      await flushPromises();

      expect(mounted.toaster.toasts.value).toHaveLength(0);

      progress.fail("Couldn't update 3 tracks", "None of the changes were saved.");
      await flushPromises();

      const [failure] = mounted.toaster.toasts.value;

      expect(failure?.color).toBe("error");
      expect(failure?.description).toBe("None of the changes were saved.");
    });
  });
});
