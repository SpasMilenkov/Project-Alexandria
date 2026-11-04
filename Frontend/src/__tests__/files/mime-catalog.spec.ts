import { describe, expect, it } from "vitest";

import {
  POLICY_FILE_GROUPS,
  canonicalizeMime,
  isAutoTagSupportedMime,
  policyGroupOf,
  routesToPreviewWorker,
} from "@/utils/mime-catalog";
import {
  MIME_META,
  getFileTypeReadable,
  isThumbnailSupportedMimeType,
} from "@/utils/mimetype.utils";

describe("canonicalizeMime", () => {
  it("strips parameters, trims, lowercases and resolves aliases", () => {
    expect(canonicalizeMime("  Text/Plain ; charset=UTF-8")).toBe("text/plain");
    expect(canonicalizeMime("AUDIO/MP3")).toBe("audio/mpeg");
    expect(canonicalizeMime("application/x-zip-compressed")).toBe("application/zip");
    expect(canonicalizeMime("text/yaml")).toBe("application/yaml");
  });

  it("returns an empty string for blank input", () => {
    expect(canonicalizeMime(null)).toBe("");
    expect(canonicalizeMime(undefined)).toBe("");
    expect(canonicalizeMime("")).toBe("");
  });
});

describe("routesToPreviewWorker", () => {
  it("covers every image, audio and video type the backend routes", () => {
    expect(routesToPreviewWorker("image/avif")).toBe(true);
    expect(routesToPreviewWorker("image/heic")).toBe(true);
    expect(routesToPreviewWorker("audio/mp4")).toBe(true);
    expect(routesToPreviewWorker("audio/x-m4a")).toBe(true);
    expect(routesToPreviewWorker("video/x-m4v")).toBe(true);
  });

  it("excludes text, archives and EPUB", () => {
    expect(routesToPreviewWorker("text/plain")).toBe(false);
    expect(routesToPreviewWorker("application/yaml")).toBe(false);
    expect(routesToPreviewWorker("application/zip")).toBe(false);
    expect(routesToPreviewWorker("application/epub+zip")).toBe(false);
  });

  it("is what the thumbnail gate uses", () => {
    expect(isThumbnailSupportedMimeType("audio/x-m4a")).toBe(true);
    expect(isThumbnailSupportedMimeType("text/x-m3u")).toBe(false);
  });
});

describe("isAutoTagSupportedMime", () => {
  it("accepts audio and Ogg, but not playlists that use an audio alias", () => {
    expect(isAutoTagSupportedMime("audio/flac")).toBe(true);
    expect(isAutoTagSupportedMime("application/ogg")).toBe(true);
    expect(isAutoTagSupportedMime("audio/x-mpegurl")).toBe(false);
  });
});

describe("policyGroupOf", () => {
  it("follows the decided groups", () => {
    expect(policyGroupOf("text/markdown")).toBe("Text");
    expect(policyGroupOf("application/vnd.android.package-archive")).toBe("Archives");
    expect(policyGroupOf("application/toml")).toBe("Code");
    expect(policyGroupOf("text/csv")).toBe("Spreadsheets");
  });

  it("does not let short entries claim unrelated types", () => {
    expect(policyGroupOf("application/x-rar-compressed")).toBe("Archives");
    expect(policyGroupOf("application/x-sharedlib")).toBeNull();
  });

  it("exposes the backend group names in declaration order", () => {
    expect(POLICY_FILE_GROUPS).toEqual([
      "Images",
      "Videos",
      "Audio",
      "Documents",
      "Spreadsheets",
      "Presentations",
      "Archives",
      "Code",
      "Text",
    ]);
  });
});

describe("MIME_META", () => {
  it("is keyed by canonical types only", () => {
    for (const key of Object.keys(MIME_META)) {
      expect(canonicalizeMime(key), key).toBe(key);
    }
  });

  it("never disagrees with the backend policy group", () => {
    for (const [key, meta] of Object.entries(MIME_META)) {
      const group = policyGroupOf(key);

      if (group !== null) {
        expect(meta.group, key).toBe(group);
      }
    }
  });

  it("labels aliases through their canonical type", () => {
    expect(getFileTypeReadable("application/javascript")).toBe("JavaScript File");
    expect(getFileTypeReadable("audio/x-m4a")).toBe("M4A Audio");
    expect(getFileTypeReadable("text/yaml")).toBe("YAML File");
  });
});
