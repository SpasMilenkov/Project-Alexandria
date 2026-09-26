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
      icon: "i-mdi-file-upload-outline",
      label: "Upload File",
      onSelect: () => actions.uploadFile(),
    },
    {
      icon: "i-mdi-folder-upload-outline",
      label: "Upload Folder",
      onSelect: () => actions.uploadDirectory(),
    },
    {
      icon: "i-formkit-zip",
      label: "Upload Archive",
      onSelect: () => actions.uploadArchive(),
    },
  ],
  [
    {
      icon: "i-mdi-folder-plus",
      label: "New Folder",
      onSelect: () => actions.createDirectory(),
    },
  ],
  [
    {
      icon: "i-mdi-refresh",
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
            icon: "i-mdi-information-outline",
            label: "View details",
            kbds: [{ value: "alt" }, { value: "enter" }],
            onSelect: () => actions.openFileDetails(target),
          },
          {
            disabled: !canDownload(),
            icon: "i-mdi-download-outline",
            kbds: [{ value: "D" }],
            label: "Download",
            onSelect: () => actions.downloadFile(),
          },
        ],
        [
          {
            disabled: !canRename(),
            icon: "i-mdi-pencil-outline",
            kbds: ["R"],
            label: "Rename",
            onSelect: () => actions.renameFile(target.fileId, target.fileName),
          },
          {
            disabled: !canMove(),
            icon: "i-mdi-folder-move-outline",
            label: "Move to…",
            kbds: ["⌘", "X"],
            onSelect: () => actions.moveSelection(),
          },
          {
            disabled: !canCopy(),
            icon: "i-mdi-content-copy",
            kbds: ["⌘", "C"],
            label: "Copy to…",
            onSelect: () => actions.copySelection(),
          },
        ],
        [
          {
            disabled: !canShare(),
            icon: "i-mdi-share-variant-outline",
            label: "Share",
            onSelect: () => actions.shareFile(target.fileId, target.fileName),
          },
        ],
        [
          {
            color: "error" as const,
            disabled: !canDelete(),
            icon: "i-mdi-delete-outline",
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
          icon: "i-mdi-download-multiple-outline",
          label: "Download all",
          onSelect: () => actions.downloadFile(),
        },
      ],
      [
        {
          disabled: !canMove(),
          icon: "i-mdi-folder-move-outline",
          label: "Move all to…",
          onSelect: () => actions.moveSelection(),
        },
        {
          disabled: !canCopy(),
          icon: "i-mdi-content-copy",
          label: "Copy all to…",
          onSelect: () => actions.copySelection(),
        },
        {
          disabled: !canShare(),
          icon: "i-mdi-share-variant-outline",
          label: "Share all",
          onSelect: () => actions.shareFile(target.fileId, target.fileName),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete(),
          icon: "i-mdi-delete-sweep-outline",
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
          icon: "i-mdi-information-outline",
          label: "View details",
          kbds: [{ value: "alt" }, { value: "enter" }],
          onSelect: () => {
            actions.openDirectoryDetails(target);
          },
        },
        {
          icon: "i-mdi-folder-open",
          label: "Open",
          onSelect: () => actions.openDirectory(target.id),
        },
      ],
      [
        {
          disabled: !canRename(),
          icon: "i-mdi-pencil-outline",
          label: "Rename",
          kbds: ["R"],
          onSelect: () => actions.renameDirectory(target.id),
        },
      ],
      [
        {
          disabled: !canMove(),
          icon: "i-mdi-folder-move-outline",
          label: "Move to…",
          kbds: ["⌘", "X"],
          onSelect: () => actions.moveSelection(),
        },
        {
          disabled: !canCopy(),
          icon: "i-mdi-content-copy",
          label: "Copy to…",
          kbds: ["⌘", "C"],
          onSelect: () => actions.copySelection(),
        },
        {
          disabled: !canDownload(),
          icon: "i-mdi-download-outline",
          label: "Download",
          kbds: ["D"],
          onSelect: () => actions.downloadDirectory(),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete(),
          icon: "i-mdi-delete-outline",
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
        icon: "i-mdi-folder-move-outline",
        label: `Move ${count} items to…`,
        kbds: ["⌘", "X"],
        onSelect: () => actions.moveSelection(),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: `Copy ${count} items to…`,
        kbds: ["⌘", "C"],
        onSelect: () => actions.copySelection(),
      },
      {
        disabled: !canDownload(),
        icon: "i-mdi-download-multiple-outline",
        label: `Download ${count} items`,
        kbds: ["D"],
        onSelect: () => actions.downloadDirectory(),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-mdi-delete-sweep-outline",
        label: `Delete ${count} items`,
        kbds: ["Del"],
        onSelect: () => actions.deleteSelection(),
      },
    ],
  ];
};

export type { ContextMenuTarget, ExplorerMenuActions, ExplorerMenuKind, ExplorerMenuSnapshot };
export { buildExplorerMenuItems, resolveContextMenuTarget };
