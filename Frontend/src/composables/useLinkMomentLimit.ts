import { useQuery } from "@pinia/colada";
import { type Ref, computed } from "vue";

import type { LinkMoment, MomentLimit } from "@/features/notes/link-picker";

import { getDocumentMetadata } from "@/queries/files";
import { getStreamingFile } from "@/queries/streaming";
import { PREVIEW_PAGE_LIMIT } from "@/utils/files/mime-catalog";

/**
 * How far a link into a file can point: a recording's length in whole seconds, or the last page
 * its preview keeps. Links open pages in the preview, which stops after the first few pages, so
 * a long PDF is still limited to those. Recordings the server hasn't measured have no limit.
 */
export const useLinkMomentLimit = (fileId: Ref<string | null>, moment: Ref<LinkMoment | null>) => {
  const { data: media } = useQuery(() => ({
    ...getStreamingFile(fileId.value ?? ""),
    enabled: moment.value === "time" && Boolean(fileId.value),
  }));

  // A null version reads the current one, which is the one a link opens.
  const { data: document } = useQuery(() => ({
    ...getDocumentMetadata({ fileId: fileId.value ?? "", versionId: null }),
    enabled: moment.value === "page" && Boolean(fileId.value),
  }));

  // The whole document's page count, when the server has read it.
  const pageTotal = computed((): number | null => {
    const pages = document.value?.pageCount ?? 0;

    return pages > 0 ? pages : null;
  });

  const limit = computed((): MomentLimit => {
    if (moment.value === "time") {
      const duration = media.value?.duration ?? 0;

      return duration > 0 ? Math.floor(duration) : null;
    }

    if (moment.value === "page") return Math.min(pageTotal.value ?? PREVIEW_PAGE_LIMIT, PREVIEW_PAGE_LIMIT);

    return null;
  });

  // Set when the document has pages past the preview, or might: links can't open those.
  const isPreviewCut = computed(
    () => moment.value === "page" && (pageTotal.value ?? Number.POSITIVE_INFINITY) > PREVIEW_PAGE_LIMIT,
  );

  return { isPreviewCut, limit };
};
