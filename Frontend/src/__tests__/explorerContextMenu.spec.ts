import { beforeEach, describe, expect, it, vi } from "vitest";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import {
  type ExplorerMenuActions,
  type ExplorerMenuSnapshot,
  buildExplorerMenuItems,
  resolveContextMenuTarget,
} from "@/utils/explorerContextMenu";

const makeFile = (fileId: string): FileResult => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  currentVersion: {
    id: "v1",
    isDeleted: false,
    isEncrypted: false,
    mimeType: "text/plain",
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId: null,
  fileId,
  fileName: `${fileId}.txt`,
  mimeType: "text/plain",
  owner: { email: "owner@test.com", id: "owner", name: "Owner" },
  tags: [],
  updatedAt: null,
});

const makeDir = (id: string): DirectorySummaryDto => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  id,
  name: `Dir ${id}`,
  ownerUserDto: { email: "owner@test.com", id: "owner", name: "Owner" },
  parentId: "root",
  updatedAt: "2026-01-02T00:00:00.000Z",
});

const spies = {
  copySelection: vi.fn(),
  createDirectory: vi.fn(),
  deleteSelection: vi.fn(),
  downloadDirectory: vi.fn(),
  downloadFile: vi.fn(),
  moveSelection: vi.fn(),
  openDirectory: vi.fn(),
  openDirectoryDetails: vi.fn(),
  openFileDetails: vi.fn(),
  refresh: vi.fn(),
  renameDirectory: vi.fn(),
  renameFile: vi.fn(),
  shareFile: vi.fn(),
  uploadArchive: vi.fn(),
  uploadDirectory: vi.fn(),
  uploadFile: vi.fn(),
};

const makeActions = (): ExplorerMenuActions => ({ ...spies });

const fileSnapshot = (fileId = "f1", totalCount = 1): ExplorerMenuSnapshot => ({
  directoryIds: [],
  fileIds: [fileId],
  kind: "file",
  targetDirectory: null,
  targetFile: makeFile(fileId),
  totalCount,
});

const dirSnapshot = (id = "d1", totalCount = 1): ExplorerMenuSnapshot => ({
  directoryIds: [id],
  fileIds: [],
  kind: "directory",
  targetDirectory: makeDir(id),
  targetFile: null,
  totalCount,
});

const backgroundSnapshot = (): ExplorerMenuSnapshot => ({
  directoryIds: [],
  fileIds: [],
  kind: "background",
  targetDirectory: null,
  targetFile: null,
  totalCount: 0,
});

const flatten = (groups: ReturnType<typeof buildExplorerMenuItems>) => groups.flat();

const labels = (groups: ReturnType<typeof buildExplorerMenuItems>) =>
  flatten(groups).map((item) => (item as { label?: string }).label);

const findItem = (groups: ReturnType<typeof buildExplorerMenuItems>, label: string) =>
  flatten(groups).find((item) => (item as { label?: string }).label === label) as {
    onSelect?: () => void;
  };

describe("buildExplorerMenuItems", () => {
  beforeEach(() => {
    Object.values(spies).forEach((spy) => spy.mockReset());
  });

  it("builds the background menu", () => {
    const actions = makeActions();
    const items = buildExplorerMenuItems(backgroundSnapshot(), actions);
    expect(labels(items)).toEqual([
      "Upload File",
      "Upload Folder",
      "Upload Archive",
      "New Folder",
      "Refresh",
    ]);
    findItem(items, "Upload File").onSelect?.();
    expect(spies.uploadFile).toHaveBeenCalledTimes(1);
    findItem(items, "Refresh").onSelect?.();
    expect(spies.refresh).toHaveBeenCalledTimes(1);
  });

  it("builds the single-file menu with shortcut hints", () => {
    const actions = makeActions();
    const items = buildExplorerMenuItems(fileSnapshot(), actions);
    expect(labels(items)).toEqual([
      "View details",
      "Download",
      "Rename",
      "Move to…",
      "Copy to…",
      "Share",
      "Delete",
    ]);
    const details = flatten(items).find(
      (item) => (item as { label?: string }).label === "View details",
    ) as { kbds?: unknown[] };
    expect(details.kbds).toEqual([{ value: "alt" }, { value: "enter" }]);

    findItem(items, "View details").onSelect?.();
    expect(spies.openFileDetails).toHaveBeenCalledWith(makeFile("f1"));
    findItem(items, "Rename").onSelect?.();
    expect(spies.renameFile).toHaveBeenCalledWith("f1", "f1.txt");
    findItem(items, "Share").onSelect?.();
    expect(spies.shareFile).toHaveBeenCalledWith("f1", "f1.txt");
    findItem(items, "Download").onSelect?.();
    expect(spies.downloadFile).toHaveBeenCalledTimes(1);
    findItem(items, "Delete").onSelect?.();
    expect(spies.deleteSelection).toHaveBeenCalledTimes(1);
  });

  it("builds the multi-file menu with selection counts", () => {
    const actions = makeActions();
    const items = buildExplorerMenuItems(fileSnapshot("f1", 3), actions);
    expect(labels(items)).toContain("3 items selected");
    expect(labels(items)).toContain("Download all");
    expect(labels(items)).toContain("Delete 3 items");
    expect(labels(items)).not.toContain("View details");
    findItem(items, "Download all").onSelect?.();
    expect(spies.downloadFile).toHaveBeenCalledTimes(1);
  });

  it("builds the single-directory menu", () => {
    const actions = makeActions();
    const items = buildExplorerMenuItems(dirSnapshot(), actions);
    expect(labels(items)).toEqual([
      "View details",
      "Open",
      "Rename",
      "Move to…",
      "Copy to…",
      "Download",
      "Delete",
    ]);
    findItem(items, "View details").onSelect?.();
    expect(spies.openDirectoryDetails).toHaveBeenCalledWith(makeDir("d1"));
    findItem(items, "Open").onSelect?.();
    expect(spies.openDirectory).toHaveBeenCalledWith("d1");
    findItem(items, "Rename").onSelect?.();
    expect(spies.renameDirectory).toHaveBeenCalledWith("d1");
  });

  it("builds the multi-directory menu without details", () => {
    const actions = makeActions();
    const items = buildExplorerMenuItems(dirSnapshot("d1", 2), actions);
    expect(labels(items)).toEqual([
      "Move 2 items to…",
      "Copy 2 items to…",
      "Download 2 items",
      "Delete 2 items",
    ]);
    findItem(items, "Download 2 items").onSelect?.();
    expect(spies.downloadDirectory).toHaveBeenCalledTimes(1);
  });

  it("freezes actions to the opening snapshot", () => {
    const actions = makeActions();
    const first = buildExplorerMenuItems(fileSnapshot("f1"), actions);
    buildExplorerMenuItems(fileSnapshot("f9"), actions);
    findItem(first, "View details").onSelect?.();
    expect(spies.openFileDetails).toHaveBeenCalledWith(makeFile("f1"));
  });
});

describe("resolveContextMenuTarget", () => {
  it("resolves nested row content to the file row", () => {
    const row = document.createElement("div");
    row.setAttribute("data-file-id", "f1");
    const button = document.createElement("button");
    const icon = document.createElement("span");
    button.appendChild(icon);
    row.appendChild(button);
    document.body.appendChild(row);

    const resolved = resolveContextMenuTarget(icon);
    expect(resolved).toEqual({ id: "f1", kind: "file", row });
    row.remove();
  });

  it("resolves inline SVG icon hits to the file row", () => {
    const row = document.createElement("div");
    row.setAttribute("data-file-id", "f1");
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    svg.appendChild(path);
    row.appendChild(svg);
    document.body.appendChild(row);

    expect(resolveContextMenuTarget(path)).toEqual({ id: "f1", kind: "file", row });
    row.remove();
  });

  it("resolves directory rows", () => {
    const row = document.createElement("div");
    row.setAttribute("data-dir-id", "d1");
    document.body.appendChild(row);
    expect(resolveContextMenuTarget(row)).toEqual({ id: "d1", kind: "directory", row });
    row.remove();
  });

  it("falls back to background outside rows", () => {
    const empty = document.createElement("div");
    document.body.appendChild(empty);
    expect(resolveContextMenuTarget(empty)).toEqual({ id: null, kind: "background", row: null });
    expect(resolveContextMenuTarget(null)).toEqual({ id: null, kind: "background", row: null });
    empty.remove();
  });
});
