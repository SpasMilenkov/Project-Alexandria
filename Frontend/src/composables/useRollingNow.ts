import { useIntervalFn } from "@vueuse/core";
import { ref } from "vue";

const ceilTo = (value: number, stepMs: number): number => Math.ceil(value / stepMs) * stepMs;

// A reactive "now" for trailing windows. Rounding up to the step keeps query keys stable
// between ticks while still covering the present, and the interval keeps windows moving.
export const useRollingNow = (stepMs = 60_000) => {
  const now = ref(ceilTo(Date.now(), stepMs));

  const tick = () => {
    now.value = ceilTo(Date.now(), stepMs);
  };

  useIntervalFn(tick, stepMs);

  return { now, tick };
};
