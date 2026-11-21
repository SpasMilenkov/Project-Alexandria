import type { DropdownMenuItem } from "@nuxt/ui";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";

type ExplorerMenuKind = "file" | "directory" | "background";

interface ExplorerMenuSnapshot {
  kind: ExplorerMenuKind;
  targetFile: FileResult | null;
  targetDirectory: DirectorySummaryDto | null;
  fileIds: string[];
  directoryIds: string[];
  totalCount: number;
}

interface ExplorerMenuActions {
  openFileDetails: (file: FileResult) => void;
  downloadFile: () => void;
  renameFile: (fileId: string, originalName: string) => void;
  moveSelection: () => void;
  copySelection: () => void;
  shareFile: (fileId: string, fileName: string) => void;
  deleteSelection: () => void;
  openDirectoryDetails: (directory: DirectorySummaryDto) => void;
  openDirectory: (directoryId: string) => void;
  renameDirectory: (directoryId: string) => void;
  favoriteDirectory: (directory: DirectorySummaryDto) => void;
  isFavoritePending: () => boolean;
  downloadDirectory: () => void;
  uploadFile: () => void;
  uploadDirectory: () => void;
  uploadArchive: () => void;
  createDirectory: () => void;
  refresh: () => void;
}

interface ContextMenuTarget {
  kind: "file" | "directory" | "background";
  id: string | null;
  row: Element | null;
}

/**
 * Maps a contextmenu event target to the row (or background) it belongs to.
 * Pure DOM walk so row components stay free of menu machinery.
 */
const resolveContextMenuTarget = (target: EventTarget | null): ContextMenuTarget => {
  // Element (not HTMLElement): row content includes inline SVG icons, and
  // right-clicks landing on them must still resolve to the row.
  if (target instanceof Element) {
    const row = target.closest("[data-file-id],[data-dir-id]");
    if (row instanceof Element) {
      const fileId = row.getAttribute("data-file-id");
      if (fileId) return { id: fileId, kind: "file", row };
      const directoryId = row.getAttribute("data-dir-id");
      if (directoryId) return { id: directoryId, kind: "directory", row };
    }
  }
  return { id: null, kind: "background", row: null };
};

const canRename = (): boolean => true;
const canMove = (): boolean => true;
const canCopy = (): boolean => true;
const canDownload = (): boolean => true;
const canShare = (): boolean => true;
const canDelete = (): boolean => true;

// The Notes folder stays at the root under its own name: notes are found by living under it.
// The server refuses these actions anyway; the menu just doesn't offer them.
const isSystemFolder = (directory: DirectorySummaryDto): boolean =>
  directory.systemKind !== null && directory.systemKind !== undefined;

/**
 * Single menu model for the whole explorer. Items are computed from the snapshot
 * captured when the menu opens, so later selection changes cannot retarget them.
 * Shapes mirror the previous per-row and background menus exactly.
 */
const buildExplorerMenuItems = (
  snapshot: ExplorerMenuSnapshot,
  actions: ExplorerMenuActions,
): DropdownMenuItem[][] => {
  if (snapshot.kind === "background") return buildBackgroundItems(actions);
  if (snapshot.kind === "file") {
    if (!snapshot.targetFile) return [];
    return buildFileItems(snapshot, snapshot.targetFile, actions);
  }
  if (!snapshot.targetDirectory) return [];
  return buildDirectoryItems(snapshot, snapshot.targetDirectory, actions);
};

const buildBackgroundItems = (actions: ExplorerMenuActions): DropdownMenuItem[][] => [
  [
    {
      icon: "i-lucide-file-up",
      label: "Upload File",
      onSelect: () => actions.uploadFile(),
    },
    {
      icon: "i-lucide-folder-up",
      label: "Upload Folder",
      onSelect: () => actions.uploadDirectory(),
    },
    {
      icon: "i-lucide-file-archive",
      label: "Upload Archive",
      onSelect: () => actions.uploadArchive(),
    },
  ],
  [
    {
      icon: "i-lucide-folder-plus",
      label: "New Folder",
      onSelect: () => actions.createDirectory(),
    },
  ],
  [
    {
      icon: "i-lucide-refresh-cw",
      label: "Refresh",
      onSelect: () => actions.refresh(),
    },
  ],
];

const buildFileItems = (
  snapshot: ExplorerMenuSnapshot,
  target: FileResult,
  actions: ExplorerMenuActions,
): DropdownMenuItem[][] => {
  const isMultiSelect = snapshot.totalCount > 1;
  const count = snapshot.totalCount;

  if (!isMultiSelect) {
    return [
      [
        {
          icon: "i-lucide-info",
          label: "View details",
          kbds: [{ value: "alt" }, { value: "enter" }],
          onSelect: () => actions.openFileDetails(target),
        },
        {
          disabled: !canDownload(),
          icon: "i-lucide-download",
          kbds: [{ value: "D" }],
          label: "Download",
          onSelect: () => actions.downloadFile(),
        },
      ],
      [
        {
          disabled: !canRename(),
          icon: "i-lucide-pencil",
          kbds: ["R"],
          label: "Rename",
          onSelect: () => actions.renameFile(target.fileId, target.fileName),
        },
        {
          disabled: !canMove(),
          icon: "i-lucide-folder-input",
          label: "Move to…",
          kbds: ["⌘", "X"],
          onSelect: () => actions.moveSelection(),
        },
        {
          disabled: !canCopy(),
          icon: "i-lucide-copy",
          kbds: ["⌘", "C"],
          label: "Copy to…",
          onSelect: () => actions.copySelection(),
        },
      ],
      [
        {
          disabled: !canShare(),
          icon: "i-lucide-share-2",
          label: "Share",
          onSelect: () => actions.shareFile(target.fileId, target.fileName),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete(),
          icon: "i-lucide-trash-2",
          kbds: ["Del"],
          label: "Delete",
          onSelect: () => actions.deleteSelection(),
        },
      ],
    ];
  }

  return [
    [{ label: `${count} items selected`, type: "label" as const }],
    [
      {
        disabled: !canDownload(),
        icon: "i-lucide-download",
        label: "Download all",
        onSelect: () => actions.downloadFile(),
      },
    ],
    [
      {
        disabled: !canMove(),
        icon: "i-lucide-folder-input",
        label: "Move all to…",
        onSelect: () => actions.moveSelection(),
      },
      {
        disabled: !canCopy(),
        icon: "i-lucide-copy",
        label: "Copy all to…",
        onSelect: () => actions.copySelection(),
      },
      {
        disabled: !canShare(),
        icon: "i-lucide-share-2",
        label: "Share all",
        onSelect: () => actions.shareFile(target.fileId, target.fileName),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-lucide-trash-2",
        label: `Delete ${count} items`,
        onSelect: () => actions.deleteSelection(),
      },
    ],
  ];
};

const buildDirectoryItems = (
  snapshot: ExplorerMenuSnapshot,
  target: DirectorySummaryDto,
  actions: ExplorerMenuActions,
): DropdownMenuItem[][] => {
  const isMultiSelect = snapshot.totalCount > 1;
  const count = snapshot.totalCount;

  if (!isMultiSelect) {
    return [
      [
        {
          icon: "i-lucide-info",
          label: "View details",
          kbds: [{ value: "alt" }, { value: "enter" }],
          onSelect: () => {
            actions.openDirectoryDetails(target);
          },
        },
        {
          icon: "i-lucide-folder-open",
          label: "Open",
          onSelect: () => actions.openDirectory(target.id),
        },
        {
          disabled: actions.isFavoritePending(),
          icon: target.isStarred ? "i-lucide-star-off" : "i-lucide-star",
          label: target.isStarred ? "Remove from favorites" : "Add to favorites",
          onSelect: () => actions.favoriteDirectory(target),
        },
      ],
      [
        {
          disabled: !canRename() || isSystemFolder(target),
          icon: "i-lucide-pencil",
          label: "Rename",
          kbds: ["R"],
          onSelect: () => actions.renameDirectory(target.id),
        },
      ],
      [
        {
          disabled: !canMove() || isSystemFolder(target),
          icon: "i-lucide-folder-input",
          label: "Move to…",
          kbds: ["⌘", "X"],
          onSelect: () => actions.moveSelection(),
        },
        {
          disabled: !canCopy(),
          icon: "i-lucide-copy",
          label: "Copy to…",
          kbds: ["⌘", "C"],
          onSelect: () => actions.copySelection(),
        },
        {
          disabled: !canDownload(),
          icon: "i-lucide-download",
          label: "Download",
          kbds: ["D"],
          onSelect: () => actions.downloadDirectory(),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete() || isSystemFolder(target),
          icon: "i-lucide-trash-2",
          label: "Delete",
          kbds: ["Del"],
          onSelect: () => actions.deleteSelection(),
        },
      ],
    ];
  }

  return [
    [
      {
        disabled: !canMove(),
        icon: "i-lucide-folder-input",
        label: `Move ${count} items to…`,
        kbds: ["⌘", "X"],
        onSelect: () => actions.moveSelection(),
      },
      {
        disabled: !canCopy(),
        icon: "i-lucide-copy",
        label: `Copy ${count} items to…`,
        kbds: ["⌘", "C"],
        onSelect: () => actions.copySelection(),
      },
      {
        disabled: !canDownload(),
        icon: "i-lucide-download",
        label: `Download ${count} items`,
        kbds: ["D"],
        onSelect: () => actions.downloadDirectory(),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-lucide-trash-2",
        label: `Delete ${count} items`,
        kbds: ["Del"],
        onSelect: () => actions.deleteSelection(),
      },
    ],
  ];
};

export type { ContextMenuTarget, ExplorerMenuActions, ExplorerMenuKind, ExplorerMenuSnapshot };
export { buildExplorerMenuItems, isSystemFolder, resolveContextMenuTarget };
