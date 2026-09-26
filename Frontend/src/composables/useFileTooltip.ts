import { type Ref, computed, onScopeDispose, ref, shallowRef } from "vue";

import type { FileResult } from "@/api/file";

type FileTooltipEnterKind = "pointer" | "focus";

interface SharedFileTooltipOptions {
  delay?: number;
  grace?: number;
  disabled?: Ref<boolean>;
}

/**
 * Explorer-owned rich file tooltip state.
 * Rows report hover/focus intent with their trigger anchor; the single shared
 * tooltip opens on the same timing as the previous per-row controllers
 * (delayed open on pointer, immediate on focus) and closes with a short grace
 * so pointer travel into the hoverable content does not dismiss it.
 * The target file is held in a shallow ref: hover targets are replaced wholesale,
 * never mutated, so there is no reason to deep-track them, and the pending-open
 * identity guard keeps working (a deep ref would proxy the object and break it).
 * All timers die with the owning explorer scope.
 */
export const useFileTooltip = (options?: SharedFileTooltipOptions) => {
  const delay = options?.delay ?? 600;
  const grace = options?.grace ?? 200;
  const disabled = options?.disabled;

  const file = shallowRef<FileResult | null>(null);
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

  const enter = (next: FileResult, el: HTMLElement, kind: FileTooltipEnterKind) => {
    if (disabled?.value) return;
    clearCloseTimer();
    file.value = next;
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
      if (file.value !== next) return;
      isOpen.value = true;
    }, delay);
  };

  const leave = (kind: FileTooltipEnterKind) => {
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
    file.value = null;
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
    file,
    leave,
    open,
    release,
  };
};

export type { FileTooltipEnterKind, SharedFileTooltipOptions };
