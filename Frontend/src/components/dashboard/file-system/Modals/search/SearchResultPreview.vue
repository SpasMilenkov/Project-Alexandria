<template>
  <!--
    No key and no transition: moving between two files or two folders keeps the
    same preview mounted and only its contents change, so nothing blinks.
  -->
  <DropTargetPreview
    v-if="target.kind === 'directory'"
    variant="inline"
    :trail="directoryTrail"
    :excluded-ids="[]"
  />

  <SearchFilePreview v-else :file="target.file" :query="query" />
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { SearchPreviewTarget } from "@/types/search-preview-target";

import DropTargetPreview, {
  type PreviewTrailSegment,
} from "@/components/dashboard/file-system/DropTargetPreview.vue";
import { directoryPath } from "@/queries/directories";

import SearchFilePreview from "./SearchFilePreview.vue";

// The searched text, so a file found by its contents can show where they matched.
const { query = "", target } = defineProps<{ query?: string; target: SearchPreviewTarget }>();

const directoryId = computed(() => (target.kind === "directory" ? target.directory.id : ""));

const pathQuery = useQuery(() => directoryPath(directoryId.value));

// Search results come from anywhere, so the trail shows where a folder lives.
// Until the path arrives the folder alone keeps the card stable.
const directoryTrail = computed<PreviewTrailSegment[]>(() => {
  if (target.kind !== "directory") {
    return [];
  }

  const self = { id: target.directory.id, name: target.directory.name };
  const parts = pathQuery.data.value?.pathParts;
  if (!parts || parts.at(-1)?.id !== self.id) {
    return [self];
  }
  return [{ id: null, name: "Home" }, ...parts];
});
</script>
