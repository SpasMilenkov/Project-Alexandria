import { useDocumentVisibility, useIntervalFn, useLocalStorage, useTimestamp } from "@vueuse/core";
import { computed, watch } from "vue";

import type { HomeWidget } from "@/types/home";

import { useHomeWidgetPreview } from "@/composables/useHomeWidgetPreview";
import { useHomeStore } from "@/stores/home";
import {
  homeFocusIdle,
  homeFocusNext,
  homeFocusRemaining,
  homeFocusToggle,
  parseHomeFocusState,
} from "@/utils/home-focus";

export const useHomeFocusTimer = (widget: () => HomeWidget) => {
  if (useHomeWidgetPreview()) {
    const state = computed(() => homeFocusIdle(widget().options));

    return {
      state,
      remaining: computed(() => state.value.remaining),
      toggle: () => undefined,
      reset: () => undefined,
      skipBreak: () => undefined,
    };
  }

  const home = useHomeStore();
  const visibility = useDocumentVisibility();
  const {
    timestamp: now,
    pause,
    resume,
  } = useTimestamp({
    scheduler: (cb) => useIntervalFn(cb, 250, { immediate: false }),
    controls: true,
  });
  const key = computed(() => `alexandria:home-focus:${home.ownerId ?? ""}:${widget().instanceId}`);
  const stored = useLocalStorage<ReturnType<typeof homeFocusIdle> | null>(key, null, {
    writeDefaults: false,
    flush: "sync",
    serializer: {
      read: (value) => {
        try {
          return parseHomeFocusState(JSON.parse(value));
        } catch {
          return null;
        }
      },
      write: (value) => JSON.stringify(value),
    },
  });

  const state = computed(() => stored.value ?? homeFocusIdle(widget().options));
  const remaining = computed(() => homeFocusRemaining(state.value, now.value));

  const reconcile = () => {
    if (stored.value?.status === "running" && homeFocusRemaining(stored.value, Date.now()) === 0)
      stored.value = { ...stored.value, status: "complete", remaining: 0, endsAt: null };
  };

  const updateClock = () => {
    now.value = Date.now();
    reconcile();

    if (home.ownerId && state.value.status === "running" && visibility.value === "visible")
      resume();
    else pause();
  };

  watch(remaining, reconcile);
  watch([() => state.value.status, key, visibility], updateClock, {
    immediate: true,
    flush: "sync",
  });

  const toggle = () => {
    if (!home.ownerId || home.isEditing) return;

    reconcile();
    let current = state.value;

    if (current.status === "complete" && current.phase === "longBreak")
      current = homeFocusIdle(widget().options);

    stored.value = homeFocusToggle(current, Date.now());
  };

  const reset = () => {
    if (!home.ownerId || home.isEditing) return;

    stored.value = null;
  };

  const skipBreak = () => {
    if (!home.ownerId || home.isEditing || state.value.phase === "focus") return;

    if (state.value.phase === "longBreak") stored.value = homeFocusIdle(widget().options);
    else stored.value = homeFocusNext(state.value);
  };

  return { state, remaining, toggle, reset, skipBreak };
};
