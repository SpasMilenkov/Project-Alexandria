import type { DateValue } from "@internationalized/date";

// A range picked in UCalendar; either end stays empty until the user chooses it.
export interface CalendarRange {
  end: DateValue | undefined;
  start: DateValue | undefined;
}
