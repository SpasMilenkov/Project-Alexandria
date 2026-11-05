import { beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent } from "vue";

type FakeOverlayEntry = {
  component: unknown;
  destroyOnClose: boolean;
  id: symbol;
  isMounted: boolean;
  isOpen: boolean;
  resolvePromise?: (value: unknown) => void;
};

const makeFakeOverlay = () => {
  const overlays: FakeOverlayEntry[] = [];
  const closeCalls: symbol[] = [];
  const unmountCalls: symbol[] = [];

  const getOverlay = (id: symbol) => {
    const found = overlays.find((entry) => entry.id === id);
    if (!found) throw new Error("Overlay not found");
    return found;
  };

  const create = (component: unknown, options?: { destroyOnClose?: boolean }) => {
    const entry: FakeOverlayEntry = {
      component,
      destroyOnClose: Boolean(options?.destroyOnClose),
      id: Symbol("overlay"),
      isMounted: false,
      isOpen: false,
    };
    overlays.push(entry);
    return {
      close: (value?: unknown) => close(entry.id, value),
      id: entry.id,
      open: (props?: unknown) => open(entry.id, props),
    };
  };

  const open = (id: symbol, _props?: unknown) => {
    const entry = getOverlay(id);
    entry.isOpen = true;
    entry.isMounted = true;
    const result = new Promise<unknown>((resolve) => {
      entry.resolvePromise = resolve;
    });
    return { id, result };
  };

  const close = (id: symbol, value?: unknown) => {
    const entry = getOverlay(id);
    closeCalls.push(id);
    entry.isOpen = false;
    if (entry.resolvePromise) {
      entry.resolvePromise(value);
      entry.resolvePromise = undefined;
    }
  };

  const unmount = (id: symbol) => {
    const entry = getOverlay(id);
    unmountCalls.push(id);
    entry.isMounted = false;
    if (entry.destroyOnClose) {
      const index = overlays.findIndex((item) => item.id === id);
      overlays.splice(index, 1);
    }
  };

  const afterLeave = (id: symbol) => {
    close(id);
    unmount(id);
  };

  return { afterLeave, close, closeCalls, create, open, overlays, unmount, unmountCalls };
};

describe("useLazyOverlay", () => {
  let fake: ReturnType<typeof makeFakeOverlay>;

  beforeEach(() => {
    fake = makeFakeOverlay();
  });

  const mountHelper = async <P extends Record<string, any>, R>(
    component: unknown,
    options?: { persistOnDispose?: boolean },
  ) => {
    const { useLazyModal } = await import("@/composables/useLazyOverlay");
    let helper!: ReturnType<typeof useLazyModal<P, R>>;
    const app = createApp(
      defineComponent({
        setup() {
          helper = useLazyModal<P, R>(component as never, options, {
            overlay: fake as never,
          });
          return () => null;
        },
      }),
    );
    app.mount(document.createElement("div"));
    return { app, helper };
  };

  it("creates zero registrations until open", async () => {
    const { helper } = await mountHelper({}, undefined);
    expect(fake.overlays.length).toBe(0);
    const opened = helper.open({});
    expect(fake.overlays.length).toBe(1);
    opened.close(true);
    await opened.result;
  });

  it("uses destroyOnClose for every registration", async () => {
    const { helper } = await mountHelper({}, undefined);
    helper.open({});
    helper.open({});
    expect(fake.overlays.length).toBe(2);
    expect(fake.overlays.every((entry) => entry.destroyOnClose)).toBe(true);
  });

  it("creates a fresh registration per open and never reuses a destroyed handle", async () => {
    const { helper } = await mountHelper<Record<string, never>, boolean>({}, undefined);
    const first = helper.open({});
    const firstId = first.id;
    first.close(true);
    await first.result;
    fake.afterLeave(firstId);
    expect(fake.overlays.length).toBe(0);

    const second = helper.open({});
    expect(second.id).not.toBe(firstId);
    expect(fake.overlays.length).toBe(1);
    second.close(false);
    await second.result;
  });

  it("cancels owned dialogs on scope disposal without touching unmount", async () => {
    const { app, helper } = await mountHelper<Record<string, never>, boolean>({}, undefined);
    const opened = helper.open({});
    let settledWith: unknown = "pending";
    void opened.result.then((value) => {
      settledWith = value;
    });
    app.unmount();
    await opened.result;
    expect(settledWith).toBeUndefined();
    expect(fake.unmountCalls.length).toBe(0);
    expect(fake.closeCalls).toContain(opened.id);
  });

  it("tolerates close of an already destroyed registration", async () => {
    const { helper } = await mountHelper<Record<string, never>, boolean>({}, undefined);
    const opened = helper.open({});
    opened.close(true);
    await opened.result;
    fake.afterLeave(opened.id);
    expect(() => helper.closeOwned()).not.toThrow();
  });

  it("keeps persistent operations alive across disposal", async () => {
    const { app, helper } = await mountHelper<Record<string, never>, boolean>(
      {},
      { persistOnDispose: true },
    );
    const opened = helper.open({});
    app.unmount();
    expect(fake.closeCalls.length).toBe(0);
    expect(fake.overlays.length).toBe(1);
    opened.close(true);
    expect(await opened.result).toBe(true);
  });

  it("cancels an open issued in the same tick as disposal", async () => {
    const { app, helper } = await mountHelper<Record<string, never>, boolean>({}, undefined);
    const opened = helper.open({});
    app.unmount();
    await opened.result;
    expect(fake.closeCalls).toContain(opened.id);
  });

  it("supports nested concurrent opens and settles each exactly once", async () => {
    const { helper } = await mountHelper<Record<string, never>, string>({}, undefined);
    const first = helper.open({});
    const second = helper.open({});
    expect(fake.overlays.length).toBe(2);

    let firstCount = 0;
    let secondCount = 0;
    void first.result.then(() => {
      firstCount += 1;
    });
    void second.result.then(() => {
      secondCount += 1;
    });

    first.close("one");
    second.close("two");
    expect(await first.result).toBe("one");
    expect(await second.result).toBe("two");
    first.close("one-again");
    expect(firstCount).toBe(1);
    expect(secondCount).toBe(1);
  });
});
