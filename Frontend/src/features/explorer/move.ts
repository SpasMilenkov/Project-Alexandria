import type { MoveRecord } from "@/composables/useExplorerDragSession";

export interface ExplorerMoveDeps {
  moveFiles: (args: {
    fileIds: string[];
    destinationId: string | null;
    originId: string | null;
  }) => Promise<unknown>;
  moveDirectories: (args: {
    directoryIds: string[];
    destinationId: string | null;
    originId: string | null;
  }) => Promise<unknown>;
}

export interface ExplorerMoveOutcome {
  errors: unknown[];
}

/** Files and folders move through separate endpoints, so both run in parallel. */
export const executeExplorerMove = async (
  record: MoveRecord,
  deps: ExplorerMoveDeps,
): Promise<ExplorerMoveOutcome> => {
  const tasks: Promise<unknown>[] = [];
  const { destinationId, originId } = record;

  if (record.fileIds.length > 0) {
    tasks.push(deps.moveFiles({ destinationId, fileIds: record.fileIds, originId }));
  }

  if (record.directoryIds.length > 0) {
    tasks.push(
      deps.moveDirectories({ destinationId, directoryIds: record.directoryIds, originId }),
    );
  }

  const results = await Promise.allSettled(tasks);
  const errors = results
    .filter((result): result is PromiseRejectedResult => result.status === "rejected")
    .map((result) => result.reason);

  return { errors };
};

export const reverseMove = (record: MoveRecord): MoveRecord => ({
  ...record,
  destinationId: record.originId,
  destinationName: record.originName,
  originId: record.destinationId,
  originName: record.destinationName,
});

export const describeMove = (record: MoveRecord): string => {
  const count = record.fileIds.length + record.directoryIds.length;

  if (count === 1) return `Moved "${record.leadName}" to ${record.destinationName}`;

  return `Moved ${count} items to ${record.destinationName}`;
};
