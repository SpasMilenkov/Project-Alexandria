<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between gap-3">
      <div class="flex items-center gap-2.5 min-w-0">
        <UIcon name="i-lucide-library-big" class="w-5 h-5 text-muted shrink-0" />
        <h2 class="text-base font-semibold truncate">System vocabulary</h2>
        <UBadge color="neutral" variant="subtle" size="sm">{{ allSystemTags.length }}</UBadge>
      </div>
    </div>

    <p class="text-xs text-gray-500 dark:text-gray-500 -mt-1">
      Seeded genre and mood tags shared across your library. Read only.
    </p>

    <div v-if="isLoading" class="space-y-2">
      <USkeleton v-for="i in 5" :key="i" class="h-11 rounded-lg" />
    </div>

    <div
      v-else-if="allSystemTags.length === 0"
      class="flex flex-col items-center justify-center py-16 text-center gap-3"
    >
      <UIcon name="i-lucide-shield-x" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No system tags yet</p>
      <p class="text-xs text-gray-600 dark:text-gray-400">
        The seeded vocabulary has not been installed for this server.
      </p>
    </div>

    <div v-else class="space-y-8">
      <div v-if="genreFamilies.length" class="space-y-3">
        <h3 class="text-xs uppercase tracking-wider text-gray-500 dark:text-gray-500">
          Genre families
        </h3>
        <div class="space-y-1.5">
          <GenreFamily
            v-for="family in genreFamilies"
            :key="family.parent.id"
            :parent="family.parent"
            :children="family.children"
          />
        </div>
      </div>

      <div v-if="orphanGenres.length" class="space-y-3">
        <h3 class="text-xs uppercase tracking-wider text-gray-500 dark:text-gray-500">
          Standalone genres
        </h3>
        <div class="flex flex-wrap gap-2.5">
          <SystemTagChip v-for="tag in orphanGenres" :key="tag.id" :tag="tag" />
        </div>
      </div>

      <div v-if="moodTags.length" class="space-y-3">
        <h3 class="text-xs uppercase tracking-wider text-gray-500 dark:text-gray-500">Mood</h3>
        <div class="flex flex-wrap gap-2.5">
          <SystemTagChip v-for="tag in moodTags" :key="tag.id" :tag="tag" />
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { TAG_FACET, type TagDto } from "@/api/tag";
import { systemTags as systemTagsQuery } from "@/queries/tags";

import GenreFamily from "./GenreFamily.vue";
import SystemTagChip from "./SystemTagChip.vue";

const { data: systemTagsData, isLoading } = useQuery(systemTagsQuery());

const allSystemTags = computed(() => systemTagsData.value || []);

const sortByName = (tags: TagDto[]) => tags.sort((a, b) => a.name.localeCompare(b.name));

const genreTags = computed(() =>
  sortByName(allSystemTags.value.filter((t) => t.facet === TAG_FACET.Genre)),
);
const moodTags = computed(() =>
  sortByName(allSystemTags.value.filter((t) => t.facet === TAG_FACET.Mood)),
);

const genreFamilies = computed(() => {
  const childrenByParent = new Map<string, TagDto[]>();
  for (const tag of genreTags.value) {
    if (tag.parentId) {
      const list = childrenByParent.get(tag.parentId) ?? [];
      list.push(tag);
      childrenByParent.set(tag.parentId, list);
    }
  }

  return genreTags.value
    .filter((tag) => !tag.parentId)
    .map((parent) => ({ parent, children: childrenByParent.get(parent.id) ?? [] }))
    .filter((family) => family.children.length > 0);
});

const orphanGenres = computed(() => {
  const familyParentIds = new Set(genreFamilies.value.map((family) => family.parent.id));
  return genreTags.value.filter((tag) => !tag.parentId && !familyParentIds.has(tag.id));
});
</script>

<style scoped></style>
