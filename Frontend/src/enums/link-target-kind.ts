// Mirrors the backend's LinkTargetKind: what an alexandria:// link points at. A note is a file.
export enum LinkTargetKind {
  File = 1,
  Folder = 2,
  Tag = 3,
  Playlist = 4,
}
