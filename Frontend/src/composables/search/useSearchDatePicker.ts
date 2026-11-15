import { getLocalTimeZone, toCalendarDate, today } from "@internationalized/date";
import { computed, ref, shallowRef } from "vue";

import type { CalendarRange } from "@/types/calendar-range";

import {
  type DateFilterKey,
  type SearchFilter,
  formatSearchDateRange,
} from "@/features/explorer/search-filters";

// The custom range picker behind "Pick dates…" on the date filters. It opens in
// place of the option list and hands back a finished filter.
export const useSearchDatePicker = (onPick: (filter: SearchFilter) => void) => {
  const calendarKey = ref<DateFilterKey | null>(null);
  // Shallow because CalendarDate's private fields do not survive deep unwrapping.
  const calendarRange = shallowRef<CalendarRange | null>(null);
  const todayDate = today(getLocalTimeZone());

  const isCalendarOpen = computed(() => calendarKey.value !== null);
  const canApplyRange = computed(() =>
    Boolean(calendarRange.value?.start && calendarRange.value?.end),
  );

  const openCalendar = (key: DateFilterKey) => {
    calendarKey.value = key;
    calendarRange.value = null;
  };

  const closeCalendar = () => {
    calendarKey.value = null;
    calendarRange.value = null;
  };

  const applyCalendar = () => {
    const key = calendarKey.value;
    const start = calendarRange.value?.start;
    const end = calendarRange.value?.end;
    if (!key || !start || !end) {
      return;
    }

    const from = toCalendarDate(start);
    const to = toCalendarDate(end);
    closeCalendar();
    onPick({ key, label: formatSearchDateRange(from, to), range: { from, to } });
  };

  return {
    applyCalendar,
    calendarKey,
    calendarRange,
    canApplyRange,
    closeCalendar,
    isCalendarOpen,
    openCalendar,
    todayDate,
  };
};
