interface PathPart {
  id: string;
}

/**
 * Where "New note" puts the note while browsing `path` (root first): the folder being browsed
 * when it is inside Notes, otherwise undefined, which the server reads as the Notes folder.
 */
export const newNoteFolderFor = (
  path: readonly PathPart[] | undefined,
  notesFolderId: string | null | undefined,
): string | undefined => {
  if (!path || path.length === 0 || !notesFolderId) return undefined;
  if (path[0]?.id !== notesFolderId) return undefined;

  return path.at(-1)?.id;
};
