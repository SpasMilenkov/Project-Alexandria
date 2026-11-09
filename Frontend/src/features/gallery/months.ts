import { GallerySort } from "@/enums/gallery-sort";

export const monthKey = (year: number, month: number) =>
  `${year}-${String(month).padStart(2, "0")}`;

// Capture times are stored as the camera's wall clock labeled UTC, so their months are UTC
// months. Upload times are real instants, so their months are the viewer's local months,
// matching how the server buckets them with the viewer's zone.
export const monthBounds = (sort: GallerySort, year: number, month: number) => {
  if (sort === GallerySort.Taken) {
    return {
      before: new Date(Date.UTC(year, month, 1)).toISOString(),
      since: new Date(Date.UTC(year, month - 1, 1)).toISOString(),
    };
  }

  return {
    before: new Date(year, month, 1).toISOString(),
    since: new Date(year, month - 1, 1).toISOString(),
  };
};

const MONTH_FORMAT = new Intl.DateTimeFormat(undefined, {
  month: "long",
  timeZone: "UTC",
  year: "numeric",
});

export const monthLabel = (year: number, month: number) =>
  MONTH_FORMAT.format(new Date(Date.UTC(year, month - 1, 1)));

const MONTH_NAME_FORMAT = new Intl.DateTimeFormat(undefined, { month: "long", timeZone: "UTC" });

// Under a year header the year would only repeat, so month headers carry the name alone.
export const monthName = (month: number) =>
  MONTH_NAME_FORMAT.format(new Date(Date.UTC(2000, month - 1, 1)));

const COUNT_FORMAT = new Intl.NumberFormat();

export const photoCountLabel = (count: number) =>
  `${COUNT_FORMAT.format(count)} ${count === 1 ? "photo" : "photos"}`;

export const viewerTimeZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
