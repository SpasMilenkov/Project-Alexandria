import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";

// One explorer tooltip serves both row kinds, so its target records which card to render.
export type ExplorerTooltipTarget =
  | { kind: "file"; file: FileResult }
  | { kind: "directory"; directory: DirectorySummaryDto };

export const tooltipTargetId = (target: ExplorerTooltipTarget) => {
  if (target.kind === "file") return target.file.fileId;
  return target.directory.id;
};

export const isTooltipTargetFor = (
  target: ExplorerTooltipTarget | null,
  kind: ExplorerTooltipTarget["kind"],
  id: string,
) => target !== null && target.kind === kind && tooltipTargetId(target) === id;
