import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";

export type SearchPreviewTarget =
  | { directory: DirectorySummaryDto; kind: "directory" }
  | { file: FileResult; kind: "file" };
