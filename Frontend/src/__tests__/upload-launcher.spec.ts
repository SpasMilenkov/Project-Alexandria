import { beforeEach, describe, expect, it, vi } from "vitest";

import { useUploadLauncher } from "@/composables/useUploadLauncher";

const modals = vi.hoisted(() => ({
  create: vi.fn(),
}));

vi.mock("@/composables/useLazyOverlay", () => ({ useLazyModal: modals.create }));

const port = () => ({
  open: vi.fn().mockReturnValue({ result: Promise.resolve(true) }),
  isScopeDisposed: vi.fn().mockReturnValue(false),
});

const mountLauncher = () => {
  const files = port();
  const directories = port();
  const archives = port();
  const choice = port();
  modals.create
    .mockReturnValueOnce(files)
    .mockReturnValueOnce(directories)
    .mockReturnValueOnce(archives)
    .mockReturnValueOnce(choice);

  return { launcher: useUploadLauncher(), files, directories, archives, choice };
};

beforeEach(() => modals.create.mockReset());

describe("shared upload launcher", () => {
  it.each(["File", "Directory", "Archive"] as const)(
    "opens %s with the selected destination",
    async (kind) => {
      const { launcher, files, directories, archives } = mountLauncher();
      const destination = { directoryId: "folder-1", directoryName: "Documents" };
      const ports = { File: files, Directory: directories, Archive: archives };

      expect(await launcher.open(kind, destination)).toBe(true);
      expect(ports[kind].open).toHaveBeenCalledWith(destination);
      expect(modals.create.mock.calls.slice(0, 3).every((call) => call[1]?.persistOnDispose)).toBe(
        true,
      );
      expect(modals.create.mock.calls[3]![1]).toBeUndefined();
    },
  );

  it("passes ordinary files to the root upload dialog", async () => {
    const { launcher, files, directories, choice } = mountLauncher();
    const entries = [new File(["hello"], "notes.txt")];

    expect(await launcher.openDrop({ dropType: "file", entries })).toBe(true);
    expect(files.open).toHaveBeenCalledWith({ droppedFiles: entries });
    expect(directories.open).not.toHaveBeenCalled();
    expect(choice.open).not.toHaveBeenCalled();
  });

  it("preserves relative paths for dropped folders and mixed files", async () => {
    const { launcher, directories, files } = mountLauncher();
    const entries = [
      { file: new File(["photo"], "photo.jpg"), relativePath: "Photos/Trip/photo.jpg" },
      { file: new File(["notes"], "notes.txt"), relativePath: "notes.txt" },
    ];

    await launcher.openDrop({ dropType: "dir", entries }, { directoryId: "destination" });

    expect(directories.open).toHaveBeenCalledWith({
      directoryId: "destination",
      droppedFiles: entries,
    });
    expect(files.open).not.toHaveBeenCalled();
  });

  it.each(["file", "archive"])("honors the ZIP %s choice", async (kind) => {
    const { launcher, choice, files, archives } = mountLauncher();
    choice.open.mockReturnValue({ result: Promise.resolve(kind) });
    const entries = [new File(["zip"], "backup.zip")];

    await launcher.openDrop({ dropType: "zip", entries });

    const selected = kind === "file" ? files : archives;
    const other = kind === "file" ? archives : files;
    expect(selected.open).toHaveBeenCalledWith({ droppedFiles: entries });
    expect(other.open).not.toHaveBeenCalled();
  });

  it("cancels ZIP selection without opening an upload dialog", async () => {
    const { launcher, choice, files, archives } = mountLauncher();
    choice.open.mockReturnValue({ result: Promise.resolve(null) });

    expect(await launcher.openDrop({ dropType: "zip", entries: [] })).toBe(false);
    expect(files.open).not.toHaveBeenCalled();
    expect(archives.open).not.toHaveBeenCalled();
  });

  it("does not open another dialog after leaving during ZIP selection", async () => {
    const { launcher, choice, files, archives } = mountLauncher();
    let resolveChoice = (_value: string) => {};
    choice.open.mockReturnValue({
      result: new Promise<string>((resolve) => {
        resolveChoice = resolve;
      }),
    });

    const result = launcher.openDrop({ dropType: "zip", entries: [] });
    choice.isScopeDisposed.mockReturnValue(true);
    resolveChoice("archive");

    expect(await result).toBe(false);
    expect(files.open).not.toHaveBeenCalled();
    expect(archives.open).not.toHaveBeenCalled();
  });
});
