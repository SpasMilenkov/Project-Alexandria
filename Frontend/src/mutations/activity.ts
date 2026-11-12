import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { directoryApi } from "@/api/directory";
import { fileApi } from "@/api/file";
import { ACTIVITY_QUERY_KEYS } from "@/queries/activities";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";
import { FILES_QUERY_KEYS } from "@/queries/files";

export type AuditRestoreTarget =
  | { kind: "file"; fileId: string }
  | { kind: "directory"; directoryId: string }
  | { kind: "version"; fileId: string; versionId: string };

const runRestore = async (target: AuditRestoreTarget): Promise<void> => {
  if (target.kind === "file") {
    await fileApi.restoreFiles([target.fileId]);
    return;
  }

  if (target.kind === "directory") {
    await directoryApi.restoreDirectories([target.directoryId]);
    return;
  }

  await fileApi.restoreVersion(target.versionId);
};

// Restores from a history row. The restore writes its own audit row, so the history goes
// stale together with the explorer listings.
export const restoreFromAudit = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: runRestore,
    onSettled() {
      queryCache.invalidateQueries({ key: FILES_QUERY_KEYS.root });
      queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
      queryCache.invalidateQueries({ key: ACTIVITY_QUERY_KEYS.root });
    },
  });
});
