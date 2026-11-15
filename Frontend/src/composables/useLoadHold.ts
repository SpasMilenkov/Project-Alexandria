import { onScopeDispose, ref, watch } from "vue";

const DEFAULT_HOLD_MS = 250;

// Reports a short window after the key changes, so a view can keep showing what
// it already had while the next item loads. Cached items arrive within the
// window and swap in one step; slow ones fall back to a loading state after it.
export const useLoadHold = (
  key: () => string | null,
  enabled: () => boolean = () => true,
  holdMs = DEFAULT_HOLD_MS,
) => {
  const isHolding = ref(false);
  let timer: ReturnType<typeof setTimeout> | null = null;

  const clearTimer = () => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
  };

  const release = () => {
    clearTimer();
    isHolding.value = false;
  };

  watch(key, () => {
    if (!enabled()) {
      return;
    }

    clearTimer();
    isHolding.value = true;
    timer = setTimeout(release, holdMs);
  });

  onScopeDispose(clearTimer);

  return { isHolding, release };
};
