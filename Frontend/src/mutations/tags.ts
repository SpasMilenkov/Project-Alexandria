import { defineMutation, useQueryCache } from "@pinia/colada";

import { tagApi } from "@/api/tag";
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

export const addTagToFile = defineMutation({
  mutation: ({ fileId, data }: { fileId: string; data: AddTagsToFileSchema }) =>
    tagApi.addTagsToFile(fileId, data),

  onSettled(data) {
    const queryCache = useQueryCache();

    if (data?.fileId) {
      queryCache.invalidateQueries({
        key: TAGS_QUERY_KEYS.getTagsForFile(data?.fileId),
      });
    }
  },
});

export const removeTagFromFile = defineMutation({
  mutation: ({ fileId, tagId }: { fileId: string; tagId: string }) =>
    tagApi.removeTagFromFile(fileId, tagId),

  onSettled(_data, _error, vars) {
    const queryCache = useQueryCache();

    if (vars?.fileId) {
      queryCache.invalidateQueries({
        key: TAGS_QUERY_KEYS.getTagsForFile(vars?.fileId),
      });
    }
  },
});
