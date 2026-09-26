import { defineMutation, useQueryCache } from "@pinia/colada";

import { tagApi } from "@/api/tag";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { TAGS_QUERY_KEYS } from "@/queries/tags";
import {
  type AddTagsToFileSchema,
  type CreateTagSchema,
  type UpdateTagSchema,
} from "@/schemas/tag";

export const createTag = defineMutation({
  mutation: (data: CreateTagSchema) => tagApi.createTag(data),
});

export const updateTag = defineMutation({
  mutation: ({ tagId, data }: { tagId: string; data: UpdateTagSchema }) =>
    tagApi.updateTag(tagId, data),
});

export const deleteTag = defineMutation({
  mutation: (tagId: string) => tagApi.deleteTag(tagId),
});

/**
 * Tag changes land in two places: the drawer's tag list and the single-file
 * detail backing its fallback. Hover tooltips read the same tags-for-file
 * query, so listings are left alone and tag edits stay two exact refetches.
 */
const invalidateFileTagState = (
  queryCache: ReturnType<typeof useQueryCache>,
  fileId: string,
) => {
  queryCache.invalidateQueries({ exact: true, key: TAGS_QUERY_KEYS.getTagsForFile(fileId) });
  queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(fileId) });
};

export const addTagToFile = defineMutation({
  mutation: ({ fileId, data }: { fileId: string; data: AddTagsToFileSchema }) =>
    tagApi.addTagsToFile(fileId, data),

  onSettled(data, _error, vars) {
    const queryCache = useQueryCache();

    const fileId = vars?.fileId ?? data?.fileId;
    if (fileId) invalidateFileTagState(queryCache, fileId);
  },
});

export const removeTagFromFile = defineMutation({
  mutation: ({ fileId, tagId }: { fileId: string; tagId: string }) =>
    tagApi.removeTagFromFile(fileId, tagId),

  onSettled(_data, _error, vars) {
    const queryCache = useQueryCache();

    if (vars?.fileId) invalidateFileTagState(queryCache, vars.fileId);
  },
});
