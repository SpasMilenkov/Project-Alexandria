import {
  createGlobalState,
  useColorMode,
  useDocumentVisibility,
  useIntervalFn,
  useLocalStorage,
} from "@vueuse/core";
import { watch } from "vue";

import { lastScheduledSwitch } from "@/features/appearance/color-schedule";

export interface ColorSchedule {
  enabled: boolean;
  lightFrom: string;
  darkFrom: string;
  // When the schedule last switched the mode. A manual toggle after that stays put
  // until the next boundary instead of being reverted on the next tick.
  appliedAt: number;
}

const STORAGE_KEY = "alexandria:color-schedule";
const TICK_MS = 60_000;

const DEFAULT_SCHEDULE: ColorSchedule = {
  appliedAt: 0,
  darkFrom: "19:00",
  enabled: false,
  lightFrom: "07:00",
};

/**
 * Time-of-day light/dark switching. Device-local by design: the right schedule depends
 * on where the device is, so it lives in localStorage and never reaches the server.
 * Writes go through VueUse's color-mode store, which `useDark` and Nuxt UI's toggle share.
 */
export const useColorSchedule = createGlobalState(() => {
  const schedule = useLocalStorage<ColorSchedule>(STORAGE_KEY, DEFAULT_SCHEDULE, {
    mergeDefaults: true,
  });
  const { store: colorMode } = useColorMode();

  const apply = (force = false) => {
    if (!schedule.value.enabled) return;

    const last = lastScheduledSwitch(new Date(), schedule.value.lightFrom, schedule.value.darkFrom);

    if (!last) return;

    if (!force && schedule.value.appliedAt >= last.at.getTime()) return;

    colorMode.value = last.phase;
    schedule.value = { ...schedule.value, appliedAt: Date.now() };
  };

  const setEnabled = (enabled: boolean) => {
    schedule.value = { ...schedule.value, enabled };

    if (enabled) apply(true);
  };

  const setTimes = (times: Pick<ColorSchedule, "lightFrom" | "darkFrom">) => {
    schedule.value = { ...schedule.value, ...times };
    apply(true);
  };

  return { apply, colorMode, schedule, setEnabled, setTimes };
});

/** Mount once at the app root: applies the schedule on load, each minute and on tab return. */
export const useColorScheduleRunner = () => {
  const { apply } = useColorSchedule();
  const visibility = useDocumentVisibility();

  apply();
  useIntervalFn(() => apply(), TICK_MS);

  watch(visibility, (state) => {
    if (state === "visible") apply();
  });
};
