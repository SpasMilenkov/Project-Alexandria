export interface ExplorerTab {
  id: string;
  title: string;
  activeDirId: string | null;
  // A file to select once the folder lists it, for tabs opened from elsewhere to show a file.
  revealFileId?: string;
}
