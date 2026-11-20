export interface IconOption {
  label: string;
  value: string;
  icon: string;
}

export const iconOptions: IconOption[] = [
  { icon: "i-lucide-tag", label: "Tag", value: "tag" },
  { icon: "i-lucide-bookmark", label: "Bookmark", value: "bookmark" },
  { icon: "i-lucide-folder", label: "Folder", value: "folder" },
  { icon: "i-lucide-file-text", label: "Document", value: "document" },
  { icon: "i-lucide-star", label: "Star", value: "star" },
  { icon: "i-lucide-heart", label: "Heart", value: "heart" },
  { icon: "i-lucide-flag", label: "Flag", value: "flag" },
  { icon: "i-lucide-circle-check", label: "Check", value: "check" },
  { icon: "i-lucide-triangle-alert", label: "Alert", value: "alert" },
  { icon: "i-lucide-user", label: "User", value: "user" },
  { icon: "i-lucide-users", label: "Group", value: "group" },
  { icon: "i-lucide-settings", label: "Settings", value: "settings" },
  { icon: "i-lucide-briefcase", label: "Work", value: "work" },
  { icon: "i-lucide-house", label: "Home", value: "home" },
  { icon: "i-lucide-calendar", label: "Calendar", value: "calendar" },
  { icon: "i-lucide-clock", label: "Clock", value: "clock" },
  { icon: "i-lucide-search", label: "Search", value: "search" },
  { icon: "i-lucide-circle-plus", label: "Plus", value: "plus" },
  { icon: "i-lucide-circle-minus", label: "Minus", value: "minus" },
  { icon: "i-lucide-trash-2", label: "Trash", value: "trash" },
  { icon: "i-lucide-square-pen", label: "Edit", value: "edit" },
  { icon: "i-lucide-link", label: "Link", value: "link" },
  { icon: "i-lucide-lock", label: "Lock", value: "lock" },
  { icon: "i-lucide-lock-open", label: "Unlock", value: "unlock" },
  { icon: "i-lucide-download", label: "Download", value: "download" },
  { icon: "i-lucide-upload", label: "Upload", value: "upload" },
  { icon: "i-lucide-bell", label: "Bell", value: "bell" },
  { icon: "i-lucide-messages-square", label: "Message", value: "message" },
  { icon: "i-lucide-image", label: "Image", value: "image" },
  { icon: "i-lucide-video", label: "Video", value: "video" },
  { icon: "i-lucide-map-pin", label: "Location", value: "location" },
] as const;

type IconValue = (typeof iconOptions)[number]["value"];
type IconName = (typeof iconOptions)[number]["icon"];

const iconMap: Record<IconValue, IconName> = iconOptions.reduce(
  (acc, { value, icon }) => {
    acc[value] = icon;
    return acc;
  },
  {} as Record<IconValue, IconName>,
);

export const getIconByValue = (value: IconValue): IconName => iconMap[value];

// Lucide has no PDF or language marks, so those keep their MDI logos.
//oxlint-disable sort-keys
export const getFileIcon = (fileName: string): string => {
  const extension = fileName.split(".").pop()?.toLowerCase() ?? "";
  const fileIconMap: Record<string, string> = {
    // Documents
    pdf: "i-mdi-file-pdf-box",
    doc: "i-lucide-file-text",
    docx: "i-lucide-file-text",
    txt: "i-lucide-file-text",
    // Spreadsheets
    xls: "i-lucide-file-spreadsheet",
    xlsx: "i-lucide-file-spreadsheet",
    csv: "i-lucide-file-spreadsheet",
    // Presentations
    ppt: "i-lucide-presentation",
    pptx: "i-lucide-presentation",
    // Images
    jpg: "i-lucide-file-image",
    jpeg: "i-lucide-file-image",
    png: "i-lucide-file-image",
    gif: "i-lucide-file-image",
    svg: "i-lucide-file-image",
    webp: "i-lucide-file-image",
    // Videos
    mp4: "i-lucide-file-play",
    avi: "i-lucide-file-play",
    mov: "i-lucide-file-play",
    mkv: "i-lucide-file-play",
    // Audio
    mp3: "i-lucide-file-music",
    wav: "i-lucide-file-music",
    flac: "i-lucide-file-music",
    // Archives
    zip: "i-lucide-folder-archive",
    rar: "i-lucide-folder-archive",
    "7z": "i-lucide-folder-archive",
    tar: "i-lucide-folder-archive",
    // Code
    js: "i-mdi-language-javascript",
    ts: "i-mdi-language-typescript",
    vue: "i-mdi-vuejs",
    jsx: "i-mdi-react",
    tsx: "i-mdi-react",
    py: "i-mdi-language-python",
    java: "i-mdi-language-java",
    html: "i-mdi-language-html5",
    css: "i-mdi-language-css3",
    json: "i-lucide-file-braces",
  };
  return fileIconMap[extension] ?? "i-lucide-file";
};
