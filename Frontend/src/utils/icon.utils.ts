export type IconOption = {
  label: string;
  value: string;
  icon: string;
};

export const iconOptions: IconOption[] = [
  { label: "Tag", value: "tag", icon: "heroicons:tag" },
  { label: "Bookmark", value: "bookmark", icon: "heroicons:bookmark" },
  { label: "Folder", value: "folder", icon: "heroicons:folder" },
  { label: "Document", value: "document", icon: "heroicons:document-text" },
  { label: "Star", value: "star", icon: "heroicons:star" },
  { label: "Heart", value: "heart", icon: "heroicons:heart" },
  { label: "Flag", value: "flag", icon: "heroicons:flag" },
  { label: "Check", value: "check", icon: "heroicons:check-circle" },
  { label: "Alert", value: "alert", icon: "heroicons:exclamation-triangle" },
  { label: "User", value: "user", icon: "heroicons:user" },
  { label: "Group", value: "group", icon: "heroicons:users" },
  { label: "Settings", value: "settings", icon: "heroicons:cog-6-tooth" },
  { label: "Work", value: "work", icon: "heroicons:briefcase" },
  { label: "Home", value: "home", icon: "heroicons:home" },
  { label: "Calendar", value: "calendar", icon: "heroicons:calendar" },
  { label: "Clock", value: "clock", icon: "heroicons:clock" },
  { label: "Search", value: "search", icon: "heroicons:magnifying-glass" },
  { label: "Plus", value: "plus", icon: "heroicons:plus-circle" },
  { label: "Minus", value: "minus", icon: "heroicons:minus-circle" },
  { label: "Trash", value: "trash", icon: "heroicons:trash" },
  { label: "Edit", value: "edit", icon: "heroicons:pencil-square" },
  { label: "Link", value: "link", icon: "heroicons:link" },
  { label: "Lock", value: "lock", icon: "heroicons:lock-closed" },
  { label: "Unlock", value: "unlock", icon: "heroicons:lock-open" },
  { label: "Download", value: "download", icon: "heroicons:arrow-down-tray" },
  { label: "Upload", value: "upload", icon: "heroicons:arrow-up-tray" },
  { label: "Bell", value: "bell", icon: "heroicons:bell" },
  {
    label: "Message",
    value: "message",
    icon: "heroicons:chat-bubble-left-right",
  },
  { label: "Image", value: "image", icon: "heroicons:photo" },
  { label: "Video", value: "video", icon: "heroicons:video-camera" },
  { label: "Location", value: "location", icon: "heroicons:map-pin" },
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

export function getIconByValue(value: IconValue): IconName {
  return iconMap[value];
}


export const getFileIcon = (fileName: string): string => {
  const extension = fileName.split(".").pop()?.toLowerCase() ?? "";
  const iconMap: Record<string, string> = {
    // Documents
    pdf: "mdi:file-pdf-box",
    doc: "mdi:file-word",
    docx: "mdi:file-word",
    txt: "mdi:file-document-outline",
    // Spreadsheets
    xls: "mdi:file-excel",
    xlsx: "mdi:file-excel",
    csv: "mdi:file-delimited-outline",
    // Presentations
    ppt: "mdi:file-powerpoint",
    pptx: "mdi:file-powerpoint",
    // Images
    jpg: "mdi:file-image",
    jpeg: "mdi:file-image",
    png: "mdi:file-image",
    gif: "mdi:file-image",
    svg: "mdi:file-image",
    webp: "mdi:file-image",
    // Videos
    mp4: "mdi:file-video",
    avi: "mdi:file-video",
    mov: "mdi:file-video",
    mkv: "mdi:file-video",
    // Audio
    mp3: "mdi:file-music",
    wav: "mdi:file-music",
    flac: "mdi:file-music",
    // Archives
    zip: "mdi:folder-zip",
    rar: "mdi:folder-zip",
    "7z": "mdi:folder-zip",
    tar: "mdi:folder-zip",
    // Code
    js: "mdi:language-javascript",
    ts: "mdi:language-typescript",
    vue: "mdi:vuejs",
    jsx: "mdi:react",
    tsx: "mdi:react",
    py: "mdi:language-python",
    java: "mdi:language-java",
    html: "mdi:language-html5",
    css: "mdi:language-css3",
    json: "mdi:code-json",
  };
  return iconMap[extension] ?? "mdi:file-outline";
};
