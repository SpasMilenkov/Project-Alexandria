import { formatBytes } from "./size.utils";

export type FileGroup =
  | "Documents"
  | "Spreadsheets"
  | "Presentations"
  | "Images"
  | "Videos"
  | "Audio"
  | "Archives"
  | "Code"
  | "Fonts"
  | "Executables"
  | "Packages"
  | "Text"
  | "Binary"
  | "Uncategorized";

interface MimeMeta {
  group: FileGroup;
  label: string;
}

//oxlint-disable sort-keys
/**
 * Single source of truth for MIME type metadata.
 * Both `getFileTypeReadable` and `groupMimeSizeRecord` derive from this.
 */
const MIME_META: Record<string, MimeMeta> = {
  // Documents
  "application/pdf": { group: "Documents", label: "PDF Document" },
  "application/msword": { group: "Documents", label: "Word Document" },
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": {
    group: "Documents",
    label: "Word Document",
  },
  "application/vnd.oasis.opendocument.text": {
    group: "Documents",
    label: "Text Document",
  },
  "application/rtf": { group: "Documents", label: "Rich Text Document" },
  "text/markdown": { group: "Documents", label: "Markdown Document" },

  // Spreadsheets
  "application/vnd.ms-excel": { group: "Spreadsheets", label: "Spreadsheet" },
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": {
    group: "Spreadsheets",
    label: "Spreadsheet",
  },
  "application/vnd.oasis.opendocument.spreadsheet": {
    group: "Spreadsheets",
    label: "Spreadsheet",
  },
  "text/csv": { group: "Spreadsheets", label: "CSV File" },
  "text/tab-separated-values": { group: "Spreadsheets", label: "TSV File" },

  // Presentations
  "application/vnd.ms-powerpoint": {
    group: "Presentations",
    label: "Presentation",
  },
  "application/vnd.openxmlformats-officedocument.presentationml.presentation": {
    group: "Presentations",
    label: "Presentation",
  },
  "application/vnd.oasis.opendocument.presentation": {
    group: "Presentations",
    label: "Presentation",
  },

  // Images
  "image/jpeg": { group: "Images", label: "JPEG Image" },
  "image/jpg": { group: "Images", label: "JPEG Image" },
  "image/png": { group: "Images", label: "PNG Image" },
  "image/gif": { group: "Images", label: "GIF Image" },
  "image/webp": { group: "Images", label: "WebP Image" },
  "image/svg+xml": { group: "Images", label: "SVG Image" },
  "image/bmp": { group: "Images", label: "Bitmap Image" },
  "image/tiff": { group: "Images", label: "TIFF Image" },
  "image/x-icon": { group: "Images", label: "Icon File" },
  "image/heic": { group: "Images", label: "HEIC Image" },
  "image/heif": { group: "Images", label: "HEIF Image" },

  // Videos
  "video/mp4": { group: "Videos", label: "MP4 Video" },
  "video/mpeg": { group: "Videos", label: "MPEG Video" },
  "video/quicktime": { group: "Videos", label: "QuickTime Video" },
  "video/x-msvideo": { group: "Videos", label: "AVI Video" },
  "video/x-matroska": { group: "Videos", label: "MKV Video" },
  "video/webm": { group: "Videos", label: "WebM Video" },
  "video/x-flv": { group: "Videos", label: "Flash Video" },
  "video/3gpp": { group: "Videos", label: "3GP Video" },

  // Audio
  "audio/mpeg": { group: "Audio", label: "MP3 Audio" },
  "audio/mp3": { group: "Audio", label: "MP3 Audio" },
  "audio/mp4": { group: "Audio", label: "MP4 Audio" },
  "audio/wav": { group: "Audio", label: "WAV Audio" },
  "audio/x-wav": { group: "Audio", label: "WAV Audio" },
  "audio/flac": { group: "Audio", label: "FLAC Audio" },
  "audio/ogg": { group: "Audio", label: "OGG Audio" },
  "audio/webm": { group: "Audio", label: "WebM Audio" },
  "audio/aac": { group: "Audio", label: "AAC Audio" },
  "audio/x-m4a": { group: "Audio", label: "M4A Audio" },

  // Archives
  "application/zip": { group: "Archives", label: "ZIP Archive" },
  "application/x-zip-compressed": { group: "Archives", label: "ZIP Archive" },
  "application/x-rar-compressed": { group: "Archives", label: "RAR Archive" },
  "application/x-7z-compressed": { group: "Archives", label: "7-Zip Archive" },
  "application/x-tar": { group: "Archives", label: "TAR Archive" },
  "application/gzip": { group: "Archives", label: "GZIP Archive" },
  "application/x-bzip2": { group: "Archives", label: "BZIP2 Archive" },
  "application/x-xz": { group: "Archives", label: "XZ Archive" },

  // Code & Development
  "text/javascript": { group: "Code", label: "JavaScript File" },
  "application/javascript": { group: "Code", label: "JavaScript File" },
  "text/typescript": { group: "Code", label: "TypeScript File" },
  "application/typescript": { group: "Code", label: "TypeScript File" },
  "text/html": { group: "Code", label: "HTML Document" },
  "text/css": { group: "Code", label: "CSS Stylesheet" },
  "application/json": { group: "Code", label: "JSON File" },
  "application/xml": { group: "Code", label: "XML File" },
  "text/xml": { group: "Code", label: "XML File" },
  "application/x-yaml": { group: "Code", label: "YAML File" },
  "text/yaml": { group: "Code", label: "YAML File" },
  "text/x-python": { group: "Code", label: "Python Script" },
  "application/x-python-code": { group: "Code", label: "Python Script" },
  "text/x-java-source": { group: "Code", label: "Java Source File" },
  "text/x-c": { group: "Code", label: "C Source File" },
  "text/x-c++": { group: "Code", label: "C++ Source File" },
  "text/x-csharp": { group: "Code", label: "C# Source File" },
  "application/x-sh": { group: "Code", label: "Shell Script" },
  "application/x-php": { group: "Code", label: "PHP Script" },
  "text/x-ruby": { group: "Code", label: "Ruby Script" },
  "text/x-go": { group: "Code", label: "Go Source File" },
  "text/x-rust": { group: "Code", label: "Rust Source File" },

  // Fonts
  "font/ttf": { group: "Fonts", label: "TrueType Font" },
  "font/otf": { group: "Fonts", label: "OpenType Font" },
  "font/woff": { group: "Fonts", label: "WOFF Font" },
  "font/woff2": { group: "Fonts", label: "WOFF2 Font" },

  // Executables
  "application/x-executable": {
    group: "Executables",
    label: "Executable File",
  },
  "application/x-msdownload": {
    group: "Executables",
    label: "Windows Executable",
  },

  // Packages
  "application/vnd.android.package-archive": {
    group: "Packages",
    label: "Android Package",
  },
  "application/x-debian-package": {
    group: "Packages",
    label: "Debian Package",
  },

  // Text
  "text/plain": { group: "Text", label: "Text File" },

  // Binary
  "application/octet-stream": { group: "Binary", label: "Binary File" },
};

// Fallback helpers

const groupFromMimeType = (mimeType: string): FileGroup => {
  const [category, subtype = ""] = mimeType.split("/");
  const sub = subtype.toLowerCase();

  if (category === "image") {
    return "Images";
  }
  if (category === "video") {
    return "Videos";
  }
  if (category === "audio") {
    return "Audio";
  }
  if (category === "font") {
    return "Fonts";
  }
  if (category === "text") {
    if (
      sub.includes("script") ||
      sub.includes("javascript") ||
      sub.includes("typescript") ||
      sub.includes("python") ||
      sub.includes("java") ||
      sub.includes("html") ||
      sub.includes("css")
    ) {
      return "Code";
    }
    return "Text";
  }
  if (
    sub.includes("wordprocessing") ||
    sub.includes("pdf") ||
    (sub.includes("document") && !sub.includes("spreadsheet") && !sub.includes("presentation"))
  ) {
    return "Documents";
  }
  if (sub.includes("spreadsheet") || sub.includes("excel") || sub.includes("sheet")) {
    return "Spreadsheets";
  }
  if (sub.includes("presentation") || sub.includes("powerpoint")) {
    return "Presentations";
  }
  if (
    sub.includes("zip") ||
    sub.includes("compressed") ||
    sub.includes("archive") ||
    sub.includes("tar") ||
    sub.includes("rar") ||
    sub.includes("gzip")
  ) {
    return "Archives";
  }
  if (
    sub.includes("script") ||
    sub.includes("json") ||
    sub.includes("yaml") ||
    sub.includes("xml")
  ) {
    return "Code";
  }

  return "Uncategorized";
};

const labelFromMimeType = (mimeType: string, fileName?: string): string => {
  const [, subtype = ""] = mimeType.split("/");
  const group = groupFromMimeType(mimeType);

  // For well-known groups, a generic label is fine
  const genericLabels: Partial<Record<FileGroup, string>> = {
    Archives: "Archive",
    Audio: "Audio File",
    Code: "Code File",
    Documents: "Document",
    Fonts: "Font File",
    Images: "Image File",
    Presentations: "Presentation",
    Spreadsheets: "Spreadsheet",
    Text: "Text File",
    Videos: "Video File",
  };
  if (genericLabels[group]) {
    return genericLabels[group]!;
  }

  // Try the file extension
  if (fileName) {
    const ext = fileName.split(".").pop()?.toUpperCase();
    if (ext && ext !== fileName.toUpperCase()) {
      return `${ext} File`;
    }
  }

  // Format the subtype as a last resort
  return (
    subtype
      .split(/[-._+]/)
      .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
      .join(" ") + " File"
  );
};

/**
 * Returns a human-readable label for a MIME type.
 * Falls back to file extension or formatted subtype when unknown.
 */
export const getFileTypeReadable = (mimeType: string, fileName?: string): string =>
  MIME_META[mimeType]?.label ?? labelFromMimeType(mimeType, fileName);

export interface GroupedMimeSizeResult {
  categories: FileGroup[];
  size: number[];
  formattedSize: string[];
}

/**
 * Aggregates a `{ mimeType: bytes }` record into named file-type groups.
 * Only groups with at least one byte are included in the result.
 */
export const groupMimeSizeRecord = (
  sizeByMimeType: Record<string, number>,
): GroupedMimeSizeResult => {
  const sizeMap = {} as Record<FileGroup, number>;

  for (const [mimeType, bytes] of Object.entries(sizeByMimeType)) {
    const group = MIME_META[mimeType]?.group ?? groupFromMimeType(mimeType);
    sizeMap[group] = (sizeMap[group] ?? 0) + bytes;
  }

  const categories: FileGroup[] = [];
  const size: number[] = [];
  const formattedSize: string[] = [];

  for (const [group, totalBytes] of Object.entries(sizeMap) as [FileGroup, number][]) {
    if (totalBytes > 0) {
      categories.push(group);
      size.push(totalBytes);
      formattedSize.push(formatBytes(totalBytes));
    }
  }

  return { categories, formattedSize, size };
};
