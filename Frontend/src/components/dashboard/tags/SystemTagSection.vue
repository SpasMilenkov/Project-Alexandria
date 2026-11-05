<template>
  <section aria-labelledby="system-vocabulary-heading">
    <header
      class="flex items-center justify-between gap-3 border-b border-gray-200/70 px-4 py-3 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-library-big"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h2
          id="system-vocabulary-heading"
          class="truncate text-base font-semibold text-gray-900 dark:text-gray-100"
        >
          System vocabulary
        </h2>
        <UBadge v-if="allSystemTags.length" color="neutral" variant="subtle" size="sm">
          {{ allSystemTags.length }}
        </UBadge>
      </div>

      <UBadge color="neutral" variant="outline" size="sm" icon="i-lucide-lock" class="shrink-0">
        Read only
      </UBadge>
    </header>

    <div class="space-y-6 p-4">
      <p class="text-sm text-gray-600 dark:text-gray-400">
        Seeded genre and mood tags shared across your library.
      </p>

      <div v-if="isPending" class="flex justify-center py-12">
        <BlockSpinner :size="24" aria-label="Loading system vocabulary" class="text-gray-500" />
      </div>

      <div
        v-else-if="allSystemTags.length === 0"
        class="flex flex-col items-center justify-center gap-3 py-12 text-center"
      >
        <UIcon name="i-lucide-shield-x" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No system tags yet</p>
        <p class="text-xs text-gray-600 dark:text-gray-400">
          The seeded vocabulary has not been installed for this server.
        </p>
      </div>

      <template v-else>
        <div v-if="genreFamilies.length" class="space-y-3">
          <h3 class="text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400">
            Genre families
          </h3>
          <div
            class="overflow-hidden rounded-lg border border-gray-200/70 divide-y divide-gray-200/70 dark:border-gray-700/70 dark:divide-gray-700/70"
          >
            <GenreFamily
              v-for="family in genreFamilies"
              :key="family.parent.id"
              :parent="family.parent"
              :children="family.children"
            />
          </div>
        </div>

        <div v-if="orphanGenres.length" class="space-y-3">
          <h3 class="text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400">
            Standalone genres
          </h3>
          <div class="flex flex-wrap gap-2">
            <SystemTagChip v-for="tag in orphanGenres" :key="tag.id" :tag="tag" />
          </div>
        </div>

        <div v-if="moodTags.length" class="space-y-3">
          <h3 class="text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400">
            Mood
          </h3>
          <div class="flex flex-wrap gap-2">
            <SystemTagChip v-for="tag in moodTags" :key="tag.id" :tag="tag" />
          </div>
        </div>
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { TAG_FACET, type TagDto } from "@/api/tag";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { systemTags as systemTagsQuery } from "@/queries/tags";

import GenreFamily from "./GenreFamily.vue";
import SystemTagChip from "./SystemTagChip.vue";

const { data: systemTagsData, isPending } = useQuery(systemTagsQuery());

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
