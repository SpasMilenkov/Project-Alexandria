import type { GallerySort } from "@/enums/gallery-sort";

import { api } from "./client";

export interface GalleryPhoto {
  fileId: string;
  versionId: string;
  name: string;
  mimeType: string;
  // Null when the file sits at the root.
  directoryId: string | null;
  // The camera's wall clock labeled UTC: read it with UTC getters, never local ones.
  takenAt: string;
  // A real instant: local getters give the viewer's time.
  uploadedAt: string;
  width: number | null;
  height: number | null;
}

export interface GalleryPage {
  items: GalleryPhoto[];
  // Opaque: pass it back unchanged, since a Date round trip would drop microseconds.
  nextCursor: string | null;
}

export interface GalleryMonth {
  year: number;
  month: number;
  count: number;
}

export interface GalleryTimeline {
  months: GalleryMonth[];
  total: number;
}

export interface GalleryPageParams {
  sort: GallerySort;
  cursor?: string;
  before?: string;
  since?: string;
  limit?: number;
}

export const galleryApi = {
  getTimeline: (sort: GallerySort, timeZone: string): Promise<GalleryTimeline> =>
    api.get<GalleryTimeline>("/gallery/timeline", { params: { sort, timeZone } }),

  getPhotos: (params: GalleryPageParams): Promise<GalleryPage> =>
    api.get<GalleryPage>("/gallery/photos", { params }),
};
