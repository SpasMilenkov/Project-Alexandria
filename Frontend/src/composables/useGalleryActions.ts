import { useQueryCache } from "@pinia/colada";
import { useRouter } from "vue-router";

import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";
import type { useGalleryMonths } from "@/composables/useGalleryMonths";

import ShareLinkModal from "@/components/dashboard/file-system/Modals/ShareLinkModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useFileDownload } from "@/composables/useFileDownload";
import { useLazyModal } from "@/composables/useLazyOverlay";
import { monthsHolding, photosSubject } from "@/features/gallery/selection";
import { deleteFiles, restoreFiles } from "@/mutations/files";
import { GALLERY_QUERY_KEYS } from "@/queries/gallery";
import { useTabStore } from "@/stores/tab";

interface GalleryActionsOptions {
  months: () => GalleryMonth[];
  loader: ReturnType<typeof useGalleryMonths>;
  // Lets the selection drop photos that just left the timeline.
  onRemoved: (fileIds: ReadonlySet<string>) => void;
}

/** Trash with undo, download and share for photos picked in the grid or open in the viewer. */
export const useGalleryActions = ({ months, loader, onRemoved }: GalleryActionsOptions) => {
  const toast = useAppToast("files");
  const queryCache = useQueryCache();
  const router = useRouter();
  const tabStore = useTabStore();
  const { downloadBulk, downloadVersion } = useFileDownload();
  const { mutateAsync: trashFiles } = deleteFiles();
  const { mutateAsync: untrashFiles } = restoreFiles();
  const shareLinkModal = useLazyModal<{ fileId: string; fileName: string }, boolean>(
    ShareLinkModal,
  );

  const openTrashAction = {
    icon: "i-lucide-arrow-up-right",
    label: "Open trash",
    onClick: () => {
      void router.push({ name: "trash" });
    },
  };

  // The timeline's month counts come from the server, so they refetch; the photos themselves
  // were already updated in place.
  const refreshCounts = () => queryCache.invalidateQueries({ key: GALLERY_QUERY_KEYS.root });

  const restore = async (photos: GalleryPhoto[], holding: GalleryMonth[]) => {
    const subject = photosSubject(photos);

    try {
      await untrashFiles(photos.map((photo) => photo.fileId));
    } catch (error) {
      toast.error(`Couldn't restore ${subject}`, error, { source: "trash" });
      return;
    }

    await refreshCounts();
    await loader.reload(holding);
    toast.success(`Restored ${subject}`, { source: "trash" });
  };

  /**
   * Moves photos to the trash. Resolves false when nothing moved, so callers stay put.
   * `beforeRemove` runs once the server agreed and before the photos leave the timeline, for
   * a viewer that has to step off the photo first.
   */
  const trash = async (photos: GalleryPhoto[], beforeRemove?: () => Promise<void> | void) => {
    if (photos.length === 0) return false;

    const ids = new Set(photos.map((photo) => photo.fileId));
    const holding = monthsHolding(months(), loader.loaded.value, ids);
    const subject = photosSubject(photos);

    try {
      await trashFiles({ ids: [...ids] });
    } catch (error) {
      toast.error(`Couldn't move ${subject} to the trash`, error, { source: "trash" });
      return false;
    }

    await beforeRemove?.();
    loader.drop(ids);
    onRemoved(ids);
    void refreshCounts();

    toast.success(`Moved ${subject} to the trash`, {
      actions: [
        {
          icon: "i-lucide-undo-2",
          label: "Undo",
          onClick: () => {
            void restore(photos, holding);
          },
        },
        openTrashAction,
      ],
      source: "trash",
    });

    return true;
  };

  const download = async (photos: GalleryPhoto[]) => {
    if (photos.length === 0) return;

    try {
      if (photos.length === 1) await downloadVersion(photos[0].versionId);
      else await downloadBulk(photos.map((photo) => photo.fileId));
    } catch (error) {
      toast.error(`Couldn't download ${photosSubject(photos)}`, error);
    }
  };

  const share = (photo: GalleryPhoto) => {
    shareLinkModal.open({ fileId: photo.fileId, fileName: photo.name });
  };

  // A new tab, so the explorer tabs already open stay where the user left them.
  const showInFolder = (photo: GalleryPhoto) => {
    tabStore.createTab(photo.directoryId, photo.fileId);
    void router.push({ name: "dashboard" });
  };

  return { download, share, showInFolder, trash };
};
