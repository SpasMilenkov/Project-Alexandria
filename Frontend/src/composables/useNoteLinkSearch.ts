import { useQuery } from "@pinia/colada";
import { refDebounced } from "@vueuse/core";
import { type Ref, computed } from "vue";

import type { FileResult } from "@/api/file";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { noteTitleOf } from "@/features/notes/note-name";
import { searchDirectory } from "@/queries/directories";
import { searchFile } from "@/queries/files";
import { getNotesFolder } from "@/queries/notes";
import { getPlaylists } from "@/queries/playlist";
import { searchTag } from "@/queries/tags";
import { directorySearchApiSchema, fileSearchApiSchema } from "@/schemas/search";
import { getFileIcon } from "@/utils/files/icons";
import { isMarkdownMimeType } from "@/utils/files/mimetype";

export interface NoteLinkMenuItem {
  type?: "label";
  label: string;
  description?: string;
  icon?: string;
  // Set on items that link to something that exists.
  target?: { kind: LinkTargetKind; id: string };
  // Set on the item that creates a note with this title.
  createTitle?: string;
}

const FILE_LIMIT = 6;
const OTHER_LIMIT = 3;
const RECENT_LIMIT = 5;
const DEBOUNCE_MS = 150;

const fileItem = (file: FileResult): NoteLinkMenuItem => {
  if (isMarkdownMimeType(file.mimeType)) {
    return {
      icon: "i-lucide-notebook-pen",
      label: noteTitleOf(file.fileName),
      target: { id: file.fileId, kind: LinkTargetKind.File },
    };
  }

  return {
    icon: getFileIcon(file.fileName),
    label: file.fileName,
    target: { id: file.fileId, kind: LinkTargetKind.File },
  };
};

const group = (label: string, items: NoteLinkMenuItem[]): NoteLinkMenuItem[] => {
  if (items.length === 0) return [];

  return [{ label, type: "label" }, ...items];
};

/**
 * What the `[[` picker offers for the text typed after it: notes and files, folders, tags and
 * playlists by name, then "New note" for that title. With nothing typed yet it lists the notes
 * edited last, so the picker opens with something useful straight away.
 */
export const useNoteLinkSearch = (term: Ref<string>, currentFileId: Ref<string>) => {
  const debounced = refDebounced(term, DEBOUNCE_MS);
  const typed = computed(() => debounced.value.trim());
  const isSearching = computed(() => typed.value !== "");

  const { data: notesFolder } = useQuery(getNotesFolder);

  const filesQuery = useQuery(() => ({
    ...searchFile(
      fileSearchApiSchema.parse({ nameContains: typed.value, pageSize: FILE_LIMIT + 1 }),
    ),
    enabled: isSearching.value,
    placeholderData: (previous) => previous,
  }));

  // Recent notes are read once per editor, not again on every focus.
  const recentQuery = useQuery(() => ({
    ...searchFile(
      fileSearchApiSchema.parse({
        pageSize: RECENT_LIMIT + 1,
        sortBy: SortBy.UpdatedAt,
        sortDirection: SortDirection.Desc,
        withinDirectoryId: notesFolder.value?.id,
      }),
    ),
    enabled: Boolean(notesFolder.value?.id),
    refetchOnWindowFocus: false,
    staleTime: 60_000,
  }));

  const foldersQuery = useQuery(() => ({
    ...searchDirectory(
      directorySearchApiSchema.parse({ nameContains: typed.value, pageSize: OTHER_LIMIT }),
    ),
    enabled: isSearching.value,
    placeholderData: (previous) => previous,
  }));

  const tagsQuery = useQuery(() => ({
    ...searchTag({ nameContains: typed.value, ownerScope: "all", page: 1, pageSize: OTHER_LIMIT }),
    enabled: isSearching.value,
    placeholderData: (previous) => previous,
  }));

  const playlistsQuery = useQuery(() => ({
    ...getPlaylists({ page: 1, pageSize: OTHER_LIMIT, query: typed.value }),
    enabled: isSearching.value,
    placeholderData: (previous) => previous,
  }));

  // The note being edited never links to itself.
  const otherFiles = (files: FileResult[] | undefined) =>
    (files ?? []).filter((file) => file.fileId !== currentFileId.value);

  const recentItems = computed(() =>
    otherFiles(recentQuery.data.value?.items).slice(0, RECENT_LIMIT).map(fileItem),
  );

  const searchItems = computed((): NoteLinkMenuItem[] => {
    const files = otherFiles(filesQuery.data.value?.items).slice(0, FILE_LIMIT).map(fileItem);

    const folders = (foldersQuery.data.value?.items ?? []).map((folder) => ({
      icon: "i-lucide-folder",
      label: folder.name,
      target: { id: folder.id, kind: LinkTargetKind.Folder },
    }));

    const tags = (tagsQuery.data.value?.items ?? []).map((tag) => ({
      icon: "i-lucide-tag",
      label: tag.name,
      target: { id: tag.id, kind: LinkTargetKind.Tag },
    }));

    const playlists = (playlistsQuery.data.value?.items ?? []).map((playlist) => ({
      icon: "i-lucide-list-music",
      label: playlist.name,
      target: { id: playlist.id, kind: LinkTargetKind.Playlist },
    }));

    return [
      ...group("Notes and files", files),
      ...group("Folders", folders),
      ...group("Tags", tags),
      ...group("Playlists", playlists),
    ];
  });

  // Follows the typed text without the search delay, so picking it right away uses every letter.
  const createItem = computed((): NoteLinkMenuItem[] => {
    const title = term.value.trim();

    if (title === "") return [];

    return [
      {
        createTitle: title,
        description: "Create it and link to it",
        icon: "i-lucide-file-plus",
        label: `New note "${title}"`,
      },
    ];
  });

  const items = computed((): NoteLinkMenuItem[] => {
    if (isSearching.value) return [...searchItems.value, ...createItem.value];

    return [...group("Recent notes", recentItems.value), ...createItem.value];
  });

  return { items };
};
