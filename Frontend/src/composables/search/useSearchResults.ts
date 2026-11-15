import { useQuery } from "@pinia/colada";
import { type Ref, computed } from "vue";

import type { SearchPreviewTarget } from "@/types/search-preview-target";

import {
  type SearchFilter,
  buildSearchRequests,
  resolveSearchScope,
} from "@/features/explorer/search-filters";
import { searchDirectory } from "@/queries/directories";
import { searchFile } from "@/queries/files";
import { directorySearchApiSchema, fileSearchApiSchema } from "@/schemas/search";
import { formatShownOfTotal } from "@/utils/count";

const PAGE_SIZE = 20;

// Stand-ins for a side that is not searched; its query stays disabled.
const emptyFileQuery = fileSearchApiSchema.parse({});
const emptyDirectoryQuery = directorySearchApiSchema.parse({});

const resultKey = (result: SearchPreviewTarget) =>
  result.kind === "file" ? `file:${result.file.fileId}` : `folder:${result.directory.id}`;

// File and folder results for the typed name and the chips. Each side is only
// searched when the filters allow it, and keeps its previous results while the
// next ones load.
export const useSearchResults = (
  nameText: Readonly<Ref<string>>,
  filters: Readonly<Ref<SearchFilter[]>>,
) => {
  const scope = computed(() => resolveSearchScope(filters.value));
  const requests = computed(() => buildSearchRequests(nameText.value, filters.value, PAGE_SIZE));

  const filesQuery = useQuery(() => ({
    ...searchFile(requests.value.files ?? emptyFileQuery),
    enabled: requests.value.files !== null,
    placeholderData: (prev) => prev,
  }));

  const directoriesQuery = useQuery(() => ({
    ...searchDirectory(requests.value.directories ?? emptyDirectoryQuery),
    enabled: requests.value.directories !== null,
    placeholderData: (prev) => prev,
  }));

  const fileResults = computed(() => {
    if (!requests.value.files) {
      return [];
    }
    return filesQuery.data.value?.items ?? [];
  });

  const directoryResults = computed(() => {
    if (!requests.value.directories) {
      return [];
    }
    return directoriesQuery.data.value?.items ?? [];
  });

  // Every result in the order it is listed: folders first, then files.
  const results = computed<SearchPreviewTarget[]>(() => [
    ...directoryResults.value.map((directory) => ({ directory, kind: "directory" as const })),
    ...fileResults.value.map((file) => ({ file, kind: "file" as const })),
  ]);

  const hasResults = computed(() => results.value.length > 0);

  const isWaiting = computed(() => {
    const filesPending = requests.value.files !== null && filesQuery.isPending.value;
    const directoriesPending =
      requests.value.directories !== null && directoriesQuery.isPending.value;
    return !hasResults.value && (filesPending || directoriesPending);
  });

  const fileCountLabel = computed(() =>
    formatShownOfTotal(fileResults.value.length, filesQuery.data.value?.totalCount),
  );

  const directoryCountLabel = computed(() =>
    formatShownOfTotal(directoryResults.value.length, directoriesQuery.data.value?.totalCount),
  );

  return {
    directoryCountLabel,
    directoryResults,
    fileCountLabel,
    fileResults,
    hasResults,
    isWaiting,
    resultKey,
    results,
    scope,
  };
};
