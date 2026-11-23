import { createGlobalState } from "@vueuse/core";
import { type MaybeRefOrGetter, computed, onScopeDispose, ref, toValue, watch } from "vue";

// App-wide rather than provided by the layout, because the theme reads it too and runs above
// every layout. A count rather than a flag, so a page that mounts before the previous one
// unmounts can't have its hold dropped by the other page letting go.
const useChromeHolds = createGlobalState(() => ref(0));

/**
 * Whether a page has asked the app to step aside: the layout hides its navigation and the theme
 * shows the plain background colour instead of the background picture.
 */
export const useAppChrome = () => {
  const holds = useChromeHolds();

  return { isHidden: computed(() => holds.value > 0) };
};

/** Asks the app to step aside while `hidden` is true, and lets go when the caller unmounts. */
export const useHideAppChrome = (hidden: MaybeRefOrGetter<boolean>) => {
  const holds = useChromeHolds();
  let isHolding = false;

  const hold = (next: boolean) => {
    if (next === isHolding) return;

    isHolding = next;
    holds.value += next ? 1 : -1;
  };

  watch(() => toValue(hidden), hold, { immediate: true });
  onScopeDispose(() => hold(false));
};
