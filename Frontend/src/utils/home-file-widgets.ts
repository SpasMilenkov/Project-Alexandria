import type { FileResult } from "@/api/file";
import type { StorageBreakdown } from "@/api/status";
import type { FileSearchQuery } from "@/schemas/search";

import { HomeRecentFilesSource, HomeRecentFilesStyle, HomeStorageStyle } from "@/enums/home-widget";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { getFileIcon } from "@/utils/icon.utils";
import { groupMimeSizeRecord } from "@/utils/mimetype.utils";
import { formatBytes } from "@/utils/size.utils";

export const HOME_STORAGE_STYLES = [
  {
    value: HomeStorageStyle.Meter,
    label: "Meter",
    description: "A stacked bar with room for the details.",
  },
  { value: HomeStorageStyle.Ring, label: "Ring", description: "A circular view of your usage." },
  {
    value: HomeStorageStyle.Waffle,
    label: "Waffle",
    description: "One hundred squares, colored by type.",
  },
  {
    value: HomeStorageStyle.Breakdown,
    label: "Breakdown",
    description: "See which types take the most space.",
  },
  {
    value: HomeStorageStyle.Tank,
    label: "Tank",
    description: "A quiet wave that follows your usage.",
  },
];

export const HOME_RECENT_FILES_STYLES = [
  {
    value: HomeRecentFilesStyle.List,
    label: "List",
    description: "Familiar icon rows with sizes and dates.",
  },
  {
    value: HomeRecentFilesStyle.Thumbnails,
    label: "Thumbnails",
    description: "A grid of previews and file names.",
  },
  {
    value: HomeRecentFilesStyle.Timeline,
    label: "Timeline",
    description: "Follow your files along a time rail.",
  },
  {
    value: HomeRecentFilesStyle.Deck,
    label: "Deck",
    description: "The newest file takes the spotlight.",
  },
  {
    value: HomeRecentFilesStyle.Table,
    label: "Table",
    description: "A compact table with a feed filter.",
  },
];

export const HOME_RECENT_FILES_SOURCES = [
  { value: HomeRecentFilesSource.Created, label: "Recently created" },
  { value: HomeRecentFilesSource.Updated, label: "Recently updated" },
];

export interface HomeStorageCategory {
  label: string;
  bytes: number;
  color: string;
  share: number;
  offset: number;
}

const categoryColors: Record<string, string> = {
  Audio: "#5B7FA6",
  Video: "#A67B8F",
  Images: "#7A9E87",
  Documents: "#C9A84C",
  Other: "#8A8999",
  Previews: "#59979C",
  Transcodes: "#967BB0",
};

export const homeStorageModel = (data: StorageBreakdown) => {
  const grouped = groupMimeSizeRecord(data.sizeByType);
  const totals: Record<string, number> = Object.fromEntries(
    Object.keys(categoryColors).map((label) => [label, 0]),
  );

  grouped.categories.forEach((group, index) => {
    let label = "Other";

    if (group === "Audio" || group === "Images") label = group;
    else if (group === "Videos") label = "Video";
    else if (["Documents", "Spreadsheets", "Presentations", "Text"].includes(group))
      label = "Documents";

    totals[label] = (totals[label] ?? 0) + (grouped.size[index] ?? 0);
  });

  totals.Previews = data.previewsSize;
  totals.Transcodes = data.transcodedSize;

  const unlimited = data.quotaBytes <= 0;
  const denominator = Math.max(unlimited ? data.usedBytes : data.quotaBytes, data.usedBytes, 1);
  let offset = 0;

  const categories: HomeStorageCategory[] = Object.entries(totals)
    .filter(([, bytes]) => bytes > 0)
    .map(([label, bytes]) => {
      const share = (bytes / denominator) * 100;
      const category = { label, bytes, color: categoryColors[label]!, share, offset };
      offset += share;

      return category;
    });

  const percent = unlimited ? null : Math.round((data.usedBytes / data.quotaBytes) * 100);
  const fill = unlimited ? 0 : Math.min(100, Math.max(0, percent ?? 0));
  const remaining = unlimited
    ? "No storage limit"
    : `${formatBytes(Math.max(0, data.quotaBytes - data.usedBytes))} available`;

  return {
    categories,
    fill,
    percent,
    remaining,
    unlimited,
    used: formatBytes(data.usedBytes),
    quota: formatBytes(data.quotaBytes),
  };
};

export const homeStorageCells = (categories: HomeStorageCategory[]): (string | null)[] => {
  const shares = categories.map((category) => ({
    color: category.color,
    exact: category.share,
    count: Math.floor(category.share),
  }));
  const target = Math.min(
    100,
    Math.round(shares.reduce((total, category) => total + category.exact, 0)),
  );
  let extra = target - shares.reduce((total, category) => total + category.count, 0);

  const ranked = [...shares].sort(
    (first, second) => second.exact - second.count - (first.exact - first.count),
  );

  for (const category of ranked) {
    if (extra <= 0) break;

    category.count += 1;
    extra -= 1;
  }

  const cells: (string | null)[] = shares
    .flatMap((category) => Array<string>(category.count).fill(category.color))
    .slice(0, 100);

  return [...cells, ...Array<null>(100 - cells.length).fill(null)];
};

export const homeRecentFilesSearch = (source: HomeRecentFilesSource): FileSearchQuery => ({
  isDeleted: false,
  onlyDeleted: false,
  isStarred: false,
  isShared: false,
  currentPage: 0,
  pageSize: 40,
  sortBy: source === HomeRecentFilesSource.Updated ? SortBy.UpdatedAt : SortBy.CreatedAt,
  sortDirection: SortDirection.Desc,
  createdAfter: null,
  createdBefore: null,
  // The lower bound excludes null update dates before paging without imposing a recent-day window.
  updatedAfter: source === HomeRecentFilesSource.Updated ? "0001-01-01" : null,
  updatedBefore: null,
  deletedAfter: null,
  deletedBefore: null,
});

export const homeRecentFileDate = (file: FileResult, source: HomeRecentFilesSource) => {
  if (source === HomeRecentFilesSource.Updated) return file.updatedAt;

  return file.createdAt;
};

export const homeFileVisual = (file: Pick<FileResult, "fileName" | "mimeType">) => {
  const { fileName, mimeType } = file;
  const icon = getFileIcon(fileName);

  if (mimeType.startsWith("image/")) return { icon, color: categoryColors.Images };
  if (mimeType.startsWith("video/")) return { icon, color: categoryColors.Video };
  if (mimeType.startsWith("audio/")) return { icon, color: categoryColors.Audio };
  if (/zip|tar|rar|7z|gzip/u.test(mimeType)) return { icon, color: categoryColors.Other };

  return { icon, color: categoryColors.Documents };
};
