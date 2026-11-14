import { type Ref, computed, onScopeDispose, ref, shallowRef } from "vue";

type TooltipEnterKind = "pointer" | "focus";

interface ExplorerTooltipOptions {
  delay?: number;
  grace?: number;
  disabled?: Ref<boolean>;
}

/**
 * Explorer-owned rich tooltip state, shared by every row whatever it shows.
 * Rows report hover/focus intent with their trigger anchor; the single shared
 * tooltip opens with a delay on pointer and immediately on focus, and closes
 * with a short grace so pointer travel into the hoverable content does not
 * dismiss it. Moving between rows swaps the target without closing.
 * The target is held in a shallow ref: hover targets are replaced wholesale,
 * never mutated, so there is no reason to deep-track them, and the pending-open
 * identity guard keeps working (a deep ref would proxy the object and break it).
 * All timers die with the owning explorer scope.
 */
export const useExplorerTooltip = <T extends object>(options?: ExplorerTooltipOptions) => {
  const delay = options?.delay ?? 600;
  const grace = options?.grace ?? 200;
  const disabled = options?.disabled;

  const target = shallowRef<T | null>(null);
  const anchor = ref<HTMLElement | null>(null);
  const isOpen = ref(false);

  let openTimer: ReturnType<typeof setTimeout> | null = null;
  let closeTimer: ReturnType<typeof setTimeout> | null = null;

  const clearOpenTimer = () => {
    if (openTimer !== null) {
      clearTimeout(openTimer);
      openTimer = null;
    }
  };

  const clearCloseTimer = () => {
    if (closeTimer !== null) {
      clearTimeout(closeTimer);
      closeTimer = null;
    }
  };

  const clearTimers = () => {
    clearOpenTimer();
    clearCloseTimer();
  };

  const open = computed({
    get: () => isOpen.value,
    set: (val: boolean) => {
      if (!val) clearTimers();
      isOpen.value = val;
    },
  });

  const enter = (next: T, el: HTMLElement, kind: TooltipEnterKind) => {
    if (disabled?.value) return;
    clearCloseTimer();
    target.value = next;
    anchor.value = el;
    if (kind === "focus") {
      clearOpenTimer();
      isOpen.value = true;
      return;
    }
    if (isOpen.value) return;
    clearOpenTimer();
    openTimer = setTimeout(() => {
      openTimer = null;
      if (anchor.value !== el || !el.isConnected) return;
      if (target.value !== next) return;
      isOpen.value = true;
    }, delay);
  };

  const leave = (kind: TooltipEnterKind) => {
    if (kind === "focus") {
      clearTimers();
      isOpen.value = false;
      return;
    }
    clearOpenTimer();
    if (!isOpen.value) return;
    clearCloseTimer();
    closeTimer = setTimeout(() => {
      closeTimer = null;
      isOpen.value = false;
    }, grace);
  };

  const contentEnter = () => {
    clearCloseTimer();
  };

  const contentLeave = () => {
    clearCloseTimer();
    isOpen.value = false;
  };

  const dismiss = () => {
    clearTimers();
    isOpen.value = false;
  };

  const release = () => {
    clearTimers();
    isOpen.value = false;
    target.value = null;
    anchor.value = null;
  };

  onScopeDispose(() => {
    clearTimers();
  });

  return {
    anchor,
    contentEnter,
    contentLeave,
    dismiss,
    enter,
    leave,
    open,
    release,
    target,
  };
};

export type { ExplorerTooltipOptions, TooltipEnterKind };
