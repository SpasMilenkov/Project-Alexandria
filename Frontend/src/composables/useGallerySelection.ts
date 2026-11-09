import { computed, shallowRef } from "vue";

import type { GalleryPhoto } from "@/api/gallery";

/**
 * Which photos are selected, by file id, so the selection survives the grid virtualizing rows
 * away and months loading in around it. The anchor is the last photo toggled on its own, which
 * a shift-click extends from.
 */
export const useGallerySelection = () => {
  const selected = shallowRef<ReadonlyMap<string, GalleryPhoto>>(new Map());
  let anchor: string | null = null;

  const count = computed(() => selected.value.size);
  const active = computed(() => count.value > 0);
  const photos = computed(() => [...selected.value.values()]);

  const isSelected = (fileId: string) => selected.value.has(fileId);

  const toggle = (photo: GalleryPhoto) => {
    const next = new Map(selected.value);

    if (next.has(photo.fileId)) next.delete(photo.fileId);
    else next.set(photo.fileId, photo);

    anchor = photo.fileId;
    selected.value = next;
  };

  const setMany = (photosToSet: GalleryPhoto[], on: boolean) => {
    const next = new Map(selected.value);

    for (const photo of photosToSet) {
      if (on) next.set(photo.fileId, photo);
      else next.delete(photo.fileId);
    }

    selected.value = next;
  };

  // A plain click while selecting starts over from this photo, as in the file explorer.
  const selectOnly = (photo: GalleryPhoto) => {
    anchor = photo.fileId;
    selected.value = new Map([[photo.fileId, photo]]);
  };

  // A shift range replaces everything, and the anchor stays, so shift-clicking on the other
  // side of it flips the range instead of growing it.
  const replaceWith = (photosToKeep: GalleryPhoto[]) => {
    selected.value = new Map(photosToKeep.map((photo) => [photo.fileId, photo]));
  };

  const anchorId = () => anchor;

  const forget = (fileIds: ReadonlySet<string>) => {
    if (anchor && fileIds.has(anchor)) anchor = null;

    setMany(
      photos.value.filter((photo) => fileIds.has(photo.fileId)),
      false,
    );
  };

  const clear = () => {
    anchor = null;
    selected.value = new Map();
  };

  return {
    active,
    anchorId,
    clear,
    count,
    forget,
    isSelected,
    photos,
    replaceWith,
    selectOnly,
    setMany,
    toggle,
  };
};
