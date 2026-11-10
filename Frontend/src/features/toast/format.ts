import { formatCount } from "@/utils/count";

// "3 files and 1 folder", "2 folders", or "1 file", so a toast says exactly what it acted on.
export const describeExplorerItems = (fileCount: number, folderCount: number): string => {
  const parts: string[] = [];

  if (fileCount > 0) parts.push(formatCount(fileCount, "file"));
  if (folderCount > 0) parts.push(formatCount(folderCount, "folder"));
  if (parts.length === 0) return "nothing";

  return parts.join(" and ");
};

export const quoted = (name: string): string => `"${name}"`;
