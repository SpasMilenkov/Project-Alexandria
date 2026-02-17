import { formatBytes } from "./size.utils";

/**
 * Maps MIME types to human-readable file type names
 * Falls back to file extension if MIME type is not recognized
 */
export const getFileTypeReadable = (
  mimeType: string,
  fileName?: string,
): string => {
  // MIME type to readable name mapping
  const mimeTypeMap: Record<string, string> = {
    // Documents
    "application/pdf": "PDF Document",
    "application/msword": "Word Document",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
      "Word Document",
    "application/vnd.oasis.opendocument.text": "Text Document",
    "application/rtf": "Rich Text Document",
    "text/plain": "Text File",
    "text/markdown": "Markdown Document",

    // Spreadsheets
    "application/vnd.ms-excel": "Spreadsheet",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
      "Spreadsheet",
    "application/vnd.oasis.opendocument.spreadsheet": "Spreadsheet",
    "text/csv": "CSV File",
    "text/tab-separated-values": "TSV File",

    // Presentations
    "application/vnd.ms-powerpoint": "Presentation",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation":
      "Presentation",
    "application/vnd.oasis.opendocument.presentation": "Presentation",

    // Images
    "image/jpeg": "JPEG Image",
    "image/jpg": "JPEG Image",
    "image/png": "PNG Image",
    "image/gif": "GIF Image",
    "image/webp": "WebP Image",
    "image/svg+xml": "SVG Image",
    "image/bmp": "Bitmap Image",
    "image/tiff": "TIFF Image",
    "image/x-icon": "Icon File",
    "image/heic": "HEIC Image",
    "image/heif": "HEIF Image",

    // Videos
    "video/mp4": "MP4 Video",
    "video/mpeg": "MPEG Video",
    "video/quicktime": "QuickTime Video",
    "video/x-msvideo": "AVI Video",
    "video/x-matroska": "MKV Video",
    "video/webm": "WebM Video",
    "video/x-flv": "Flash Video",
    "video/3gpp": "3GP Video",

    // Audio
    "audio/mpeg": "MP3 Audio",
    "audio/mp3": "MP3 Audio",
    "audio/mp4": "MP4 Audio",
    "audio/wav": "WAV Audio",
    "audio/x-wav": "WAV Audio",
    "audio/flac": "FLAC Audio",
    "audio/ogg": "OGG Audio",
    "audio/webm": "WebM Audio",
    "audio/aac": "AAC Audio",
    "audio/x-m4a": "M4A Audio",

    // Archives
    "application/zip": "ZIP Archive",
    "application/x-zip-compressed": "ZIP Archive",
    "application/x-rar-compressed": "RAR Archive",
    "application/x-7z-compressed": "7-Zip Archive",
    "application/x-tar": "TAR Archive",
    "application/gzip": "GZIP Archive",
    "application/x-bzip2": "BZIP2 Archive",
    "application/x-xz": "XZ Archive",

    // Code & Development / Text
    "text/javascript": "JavaScript File",
    "application/javascript": "JavaScript File",
    "text/typescript": "TypeScript File",
    "application/typescript": "TypeScript File",
    "text/html": "HTML Document",
    "text/css": "CSS Stylesheet",
    "application/json": "JSON File",
    "application/xml": "XML File",
    "text/xml": "XML File",
    "application/x-yaml": "YAML File",
    "text/yaml": "YAML File",
    "text/x-python": "Python Script",
    "application/x-python-code": "Python Script",
    "text/x-java-source": "Java Source File",
    "text/x-c": "C Source File",
    "text/x-c++": "C++ Source File",
    "text/x-csharp": "C# Source File",
    "application/x-sh": "Shell Script",
    "application/x-php": "PHP Script",
    "text/x-ruby": "Ruby Script",
    "text/x-go": "Go Source File",
    "text/x-rust": "Rust Source File",

    // Fonts
    "font/ttf": "TrueType Font",
    "font/otf": "OpenType Font",
    "font/woff": "WOFF Font",
    "font/woff2": "WOFF2 Font",

    // Other common types
    "application/octet-stream": "Binary File",
    "application/x-executable": "Executable File",
    "application/x-msdownload": "Windows Executable",
    "application/vnd.android.package-archive": "Android Package",
    "application/x-debian-package": "Debian Package",
  };

  // Check if we have a direct match
  if (mimeTypeMap[mimeType]) {
    return mimeTypeMap[mimeType];
  }

  // Try to extract category from MIME type
  const [category, subtype] = mimeType.split("/");

  // Pattern-based matching for common document types
  if (subtype) {
    const lowerSubtype = subtype.toLowerCase();

    // Check for document-related patterns
    if (
      lowerSubtype.includes("wordprocessing") ||
      lowerSubtype.includes("document")
    ) {
      return "Document";
    }

    // Check for spreadsheet-related patterns
    if (
      lowerSubtype.includes("spreadsheet") ||
      lowerSubtype.includes("excel") ||
      lowerSubtype.includes("sheet")
    ) {
      return "Spreadsheet";
    }

    // Check for presentation-related patterns
    if (
      lowerSubtype.includes("presentation") ||
      lowerSubtype.includes("powerpoint") ||
      lowerSubtype.includes("slides")
    ) {
      return "Presentation";
    }

    // Check for PDF-related patterns
    if (lowerSubtype.includes("pdf")) {
      return "PDF Document";
    }

    // Check for archive-related patterns
    if (
      lowerSubtype.includes("zip") ||
      lowerSubtype.includes("compressed") ||
      lowerSubtype.includes("archive") ||
      lowerSubtype.includes("tar") ||
      lowerSubtype.includes("rar") ||
      lowerSubtype.includes("gzip")
    ) {
      return "Archive";
    }

    // Check for code/script patterns
    if (
      lowerSubtype.includes("script") ||
      lowerSubtype.includes("javascript") ||
      lowerSubtype.includes("typescript") ||
      lowerSubtype.includes("python") ||
      lowerSubtype.includes("java")
    ) {
      return "Code File";
    }

    // Check for image format patterns
    if (
      lowerSubtype.includes("image") ||
      lowerSubtype.includes("jpeg") ||
      lowerSubtype.includes("png") ||
      lowerSubtype.includes("gif")
    ) {
      return "Image File";
    }

    // Check for video patterns
    if (
      lowerSubtype.includes("video") ||
      lowerSubtype.includes("movie") ||
      lowerSubtype.includes("film")
    ) {
      return "Video File";
    }

    // Check for audio patterns
    if (
      lowerSubtype.includes("audio") ||
      lowerSubtype.includes("sound") ||
      lowerSubtype.includes("music")
    ) {
      return "Audio File";
    }
  }

  // Generic category fallbacks
  if (category === "image") return "Image File";
  if (category === "video") return "Video File";
  if (category === "audio") return "Audio File";
  if (category === "text") return "Text File";
  if (category === "font") return "Font File";

  // If we have a filename, try to get extension
  if (fileName) {
    const extension = fileName.split(".").pop()?.toUpperCase();
    if (extension && extension !== fileName.toUpperCase()) {
      return `${extension} File`;
    }
  }

  // Last resort: format the subtype nicely
  if (subtype) {
    const formatted = subtype
      .split(/[-._+]/)
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");
    return `${formatted} File`;
  }

  // Ultimate fallback
  return "File";
};

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

export interface GroupedMimeSizeResult {
  categories: FileGroup[];
  size: number[];
  formattedSize: string[];
}

export const groupMimeSizeRecord = (
  sizeByMimeType: Record<string, number>,
): GroupedMimeSizeResult => {
  const sizeMap: Record<FileGroup, number> = {
    Documents: 0,
    Spreadsheets: 0,
    Presentations: 0,
    Images: 0,
    Videos: 0,
    Audio: 0,
    Archives: 0,
    Code: 0,
    Fonts: 0,
    Executables: 0,
    Packages: 0,
    Text: 0,
    Binary: 0,
    Uncategorized: 0,
  };

  const mimeGroupMap: Record<string, FileGroup> = {
    "application/pdf": "Documents",
    "application/msword": "Documents",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
      "Documents",
    "application/vnd.oasis.opendocument.text": "Documents",
    "application/rtf": "Documents",
    "text/markdown": "Documents",

    "application/vnd.ms-excel": "Spreadsheets",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
      "Spreadsheets",
    "application/vnd.oasis.opendocument.spreadsheet": "Spreadsheets",
    "text/csv": "Spreadsheets",
    "text/tab-separated-values": "Spreadsheets",

    "application/vnd.ms-powerpoint": "Presentations",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation":
      "Presentations",
    "application/vnd.oasis.opendocument.presentation": "Presentations",

    "image/jpeg": "Images",
    "image/png": "Images",
    "image/gif": "Images",
    "image/webp": "Images",
    "image/svg+xml": "Images",
    "image/bmp": "Images",
    "image/tiff": "Images",
    "image/heic": "Images",
    "image/heif": "Images",
    "image/x-icon": "Images",

    "video/mp4": "Videos",
    "video/mpeg": "Videos",
    "video/quicktime": "Videos",
    "video/x-msvideo": "Videos",
    "video/x-matroska": "Videos",
    "video/webm": "Videos",
    "video/x-flv": "Videos",
    "video/3gpp": "Videos",

    "audio/mpeg": "Audio",
    "audio/mp3": "Audio",
    "audio/mp4": "Audio",
    "audio/wav": "Audio",
    "audio/x-wav": "Audio",
    "audio/flac": "Audio",
    "audio/ogg": "Audio",
    "audio/webm": "Audio",
    "audio/aac": "Audio",
    "audio/x-m4a": "Audio",

    "application/zip": "Archives",
    "application/x-zip-compressed": "Archives",
    "application/x-rar-compressed": "Archives",
    "application/x-7z-compressed": "Archives",
    "application/x-tar": "Archives",
    "application/gzip": "Archives",
    "application/x-bzip2": "Archives",
    "application/x-xz": "Archives",

    "text/javascript": "Code",
    "application/javascript": "Code",
    "text/typescript": "Code",
    "application/typescript": "Code",
    "application/json": "Code",
    "application/xml": "Code",
    "text/xml": "Code",
    "application/x-yaml": "Code",
    "text/yaml": "Code",
    "text/x-python": "Code",
    "text/html": "Code",
    "text/css": "Code",

    "font/ttf": "Fonts",
    "font/otf": "Fonts",
    "font/woff": "Fonts",
    "font/woff2": "Fonts",

    "application/octet-stream": "Binary",
    "text/plain": "Text",
  };

  // Aggregate sizes
  for (const [mimeType, size] of Object.entries(sizeByMimeType)) {
    const group =
      mimeGroupMap[mimeType] ??
      (mimeType.startsWith("image/")
        ? "Images"
        : mimeType.startsWith("video/")
          ? "Videos"
          : mimeType.startsWith("audio/")
            ? "Audio"
            : mimeType.startsWith("text/")
              ? "Text"
              : "Uncategorized");

    sizeMap[group] += size;
  }

  const categories: FileGroup[] = [];
  const size: number[] = [];
  const formattedSize: string[] = [];

  (Object.keys(sizeMap) as FileGroup[]).forEach((group) => {
    const totalBytes = sizeMap[group];

    if (totalBytes > 0) {
      categories.push(group);
      size.push(totalBytes);
      formattedSize.push(formatBytes(totalBytes));
    }
  });

  return { categories, size, formattedSize };
};
