export type SiteIconPlace =
  | "home"
  | "files"
  | "tags"
  | "trash"
  | "storage"
  | "history"
  | "music"
  | "videos"
  | "jobs"
  | "stats"
  | "wrapped"
  | "settings"
  | "account"
  | "admin"
  | "share"
  | "status";

// Solid variants only: outline glyphs lose their strokes at 16px.
export const PLACE_GLYPHS: Record<SiteIconPlace, string> = {
  account: "heroicons:user-solid",
  admin: "heroicons:server-stack-solid",
  files: "heroicons:folder-solid",
  history: "heroicons:clock-solid",
  home: "heroicons:book-open-solid",
  jobs: "heroicons:queue-list-solid",
  music: "heroicons:musical-note-solid",
  settings: "heroicons:cog-6-tooth-solid",
  share: "heroicons:link-solid",
  stats: "heroicons:chart-bar-solid",
  status: "heroicons:signal-solid",
  storage: "heroicons:circle-stack-solid",
  tags: "heroicons:tag-solid",
  trash: "heroicons:trash-solid",
  videos: "heroicons:film-solid",
  wrapped: "heroicons:sparkles-solid",
};

// Playback uses a speaker, not play/pause: transport glyphs read as buttons to press,
// which looks inverted next to the player's own controls.
export const ACTIVITY_GLYPHS = {
  extract: "heroicons:archive-box-solid",
  failed: "heroicons:exclamation-triangle-solid",
  playing: "heroicons:speaker-wave-solid",
  upload: "heroicons:arrow-up-solid",
} as const;
