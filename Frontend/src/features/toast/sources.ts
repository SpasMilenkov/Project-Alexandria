export interface ToastSourceMeta {
  icon: string;
  label: string;
}

// Each toast names the part of the app that raised it, so a stack of toasts stays tellable apart.
export const TOAST_SOURCES = {
  account: { icon: "i-lucide-user-round", label: "Account" },
  activity: { icon: "i-lucide-history", label: "Activity" },
  auth: { icon: "i-lucide-log-in", label: "Sign in" },
  clipboard: { icon: "i-lucide-clipboard-check", label: "Clipboard" },
  files: { icon: "i-lucide-file", label: "Files" },
  folders: { icon: "i-lucide-folder", label: "Folders" },
  general: { icon: "i-lucide-info", label: "Alexandria" },
  jobs: { icon: "i-lucide-layers", label: "Transcoding" },
  lyrics: { icon: "i-lucide-align-left", label: "Lyrics" },
  metadata: { icon: "i-lucide-pencil-line", label: "Track details" },
  monitoring: { icon: "i-lucide-activity", label: "Monitoring" },
  move: { icon: "i-lucide-folder-tree", label: "Move" },
  playback: { icon: "i-lucide-audio-lines", label: "Playback" },
  playlists: { icon: "i-lucide-list-music", label: "Playlists" },
  policy: { icon: "i-lucide-shield-check", label: "Policies" },
  previews: { icon: "i-lucide-image", label: "Previews" },
  report: { icon: "i-lucide-send", label: "Feedback" },
  share: { icon: "i-lucide-share-2", label: "Share links" },
  storage: { icon: "i-lucide-hard-drive", label: "Storage" },
  tags: { icon: "i-lucide-tags", label: "Tags" },
  trash: { icon: "i-lucide-trash-2", label: "Trash" },
  upload: { icon: "i-lucide-upload", label: "Uploads" },
  users: { icon: "i-lucide-users", label: "Users" },
  versions: { icon: "i-lucide-history", label: "Version history" },
  wrapped: { icon: "i-lucide-chart-line", label: "Wrapped" },
} as const satisfies Record<string, ToastSourceMeta>;

export type ToastSource = keyof typeof TOAST_SOURCES;
