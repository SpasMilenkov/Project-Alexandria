import  {type Component, onScopeDispose } from "vue";

interface OpenOptions {
  persistOnDispose?: boolean;
}

interface OpenedLazyOverlay<R> {
  id: symbol;
  result: Promise<R>;
  close: (value?: R) => void;
}

const isOverlayNotFound = (err: unknown): boolean =>
  err instanceof Error && err.message === "Overlay not found";

interface OverlayPort {
  close: (id: symbol, value?: unknown) => void;
  create: (
    component: Component,
    options?: { destroyOnClose?: boolean },
  ) => {
    close: (value?: unknown) => void;
    id: symbol;
    open: (props?: unknown) => { id: symbol; result: Promise<unknown> };
  };
}

interface LazyDeps {
  overlay?: OverlayPort;
}

/**
 * Per-caller lazy overlay opener.
 * Creates a fresh Nuxt UI registration for each actual open with
 * destroyOnClose so never-opened handles never accumulate.
 * Ordinary dialogs cancel on caller disposal. Persistent operations
 * survive disposal and settle on their own.
 */
export const useLazyModal = <P extends Record<string, any>, R = any>(
  component: Component,
  modalOptions?: OpenOptions,
  deps?: LazyDeps,
) => {
  const overlay = deps?.overlay ?? useOverlay();
  const owned = new Set<symbol>();
  const persistByDefault = modalOptions?.persistOnDispose ?? false;
  let scopeDisposed = false;

  const untrack = (id: symbol) => {
    owned.delete(id);
  };

  const safeClose = (id: symbol, value?: R) => {
    try {
      overlay.close(id, value);
    } catch (err: unknown) {
      if (!isOverlayNotFound(err)) throw err;
    }
  };

  const open = (props?: P, openOptions?: OpenOptions): OpenedLazyOverlay<R> => {
    const persist = openOptions?.persistOnDispose ?? persistByDefault;
    const handle = overlay.create(component, { destroyOnClose: true });
    const id = handle.id as symbol;

    if (!persist) owned.add(id);

    let opened: ReturnType<typeof handle.open>;
    try {
      opened = handle.open(props);
    } catch (err: unknown) {
      untrack(id);
      throw err;
    }

    let settled = false;
    const settleOnce = () => {
      if (settled) return;
      settled = true;
      untrack(id);
    };

    const result = (opened.result as Promise<R>).then(
      (value) => {
        settleOnce();
        return value;
      },
      (err: unknown) => {
        settleOnce();
        throw err;
      },
    );

    if (scopeDisposed && !persist) {
      safeClose(id);
    }

    return {
      close: (value?: R) => {
        safeClose(id, value);
      },
      id,
      result,
    };
  };

  const closeOwned = () => {
    scopeDisposed = true;
    const ids = [...owned];
    ids.forEach((id) => safeClose(id));
    owned.clear();
  };

  onScopeDispose(() => {
    closeOwned();
  });

  return { closeOwned, isScopeDisposed: () => scopeDisposed, open };
};

export type { LazyDeps, OpenedLazyOverlay, OpenOptions, OverlayPort };
