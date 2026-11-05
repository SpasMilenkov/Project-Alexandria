export interface IconOption {
  label: string;
  value: string;
  icon: string;
}

export const iconOptions: IconOption[] = [
  { icon: "heroicons:tag", label: "Tag", value: "tag" },
  { icon: "heroicons:bookmark", label: "Bookmark", value: "bookmark" },
  { icon: "heroicons:folder", label: "Folder", value: "folder" },
  { icon: "heroicons:document-text", label: "Document", value: "document" },
  { icon: "heroicons:star", label: "Star", value: "star" },
  { icon: "heroicons:heart", label: "Heart", value: "heart" },
  { icon: "heroicons:flag", label: "Flag", value: "flag" },
  { icon: "heroicons:check-circle", label: "Check", value: "check" },
  { icon: "heroicons:exclamation-triangle", label: "Alert", value: "alert" },
  { icon: "heroicons:user", label: "User", value: "user" },
  { icon: "heroicons:users", label: "Group", value: "group" },
  { icon: "heroicons:cog-6-tooth", label: "Settings", value: "settings" },
  { icon: "heroicons:briefcase", label: "Work", value: "work" },
  { icon: "heroicons:home", label: "Home", value: "home" },
  { icon: "heroicons:calendar", label: "Calendar", value: "calendar" },
  { icon: "heroicons:clock", label: "Clock", value: "clock" },
  { icon: "heroicons:magnifying-glass", label: "Search", value: "search" },
  { icon: "heroicons:plus-circle", label: "Plus", value: "plus" },
  { icon: "heroicons:minus-circle", label: "Minus", value: "minus" },
  { icon: "heroicons:trash", label: "Trash", value: "trash" },
  { icon: "heroicons:pencil-square", label: "Edit", value: "edit" },
  { icon: "heroicons:link", label: "Link", value: "link" },
  { icon: "heroicons:lock-closed", label: "Lock", value: "lock" },
  { icon: "heroicons:lock-open", label: "Unlock", value: "unlock" },
  { icon: "heroicons:arrow-down-tray", label: "Download", value: "download" },
  { icon: "heroicons:arrow-up-tray", label: "Upload", value: "upload" },
  { icon: "heroicons:bell", label: "Bell", value: "bell" },
  {
    icon: "heroicons:chat-bubble-left-right",
    label: "Message",
    value: "message",
  },
  { icon: "heroicons:photo", label: "Image", value: "image" },
  { icon: "heroicons:video-camera", label: "Video", value: "video" },
  { icon: "heroicons:map-pin", label: "Location", value: "location" },
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

//oxlint-disable sort-keys
export const getFileIcon = (fileName: string): string => {
  const extension = fileName.split(".").pop()?.toLowerCase() ?? "";
  const fileIconMap: Record<string, string> = {
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
  return fileIconMap[extension] ?? "mdi:file-outline";
};
