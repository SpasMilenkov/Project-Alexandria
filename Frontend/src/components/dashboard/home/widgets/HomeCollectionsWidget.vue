<script setup lang="ts">
import { computed } from "vue";
import { RouterLink } from "vue-router";

import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { homeCollections } from "@/queries/home-media";
import { useHomeStore } from "@/stores/home";

import HomeCollectionCover from "./HomeCollectionCover.vue";

defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const query = useHomeWidgetQuery(
  () => homeCollections({ userId: store.ownerId ?? "", epoch: store.ownerEpoch }),
  (preview) => ({
    userId: store.ownerId ?? "",
    epoch: store.ownerEpoch,
    value: preview.collections,
  }),
);

const collections = computed(() => {
  const response = query.data.value;

  if (response?.userId !== store.ownerId || response.epoch !== store.ownerEpoch) return null;

  return response.value;
});

const authPending = computed(() => isAuthError(query.error.value));
const location = { name: "playlists", query: { origin: "auto" } };
const sleevePositions = [
  { left: "4%", top: "12%", transform: "rotate(-8deg)" },
  { left: "26%", top: "2%", transform: "rotate(5deg)" },
  { left: "48%", top: "16%", transform: "rotate(-3deg)" },
  { left: "22%", top: "34%", transform: "rotate(8deg)" },
];
</script>

<template>
  <div
    class="collections-widget flex min-h-0 flex-1 flex-col"
    :class="{ 'pointer-events-none': store.isEditing }"
  >
    <HomeWidgetDataState
      :loading="authPending || (!collections && !query.error.value)"
      :failed="Boolean(query.error.value) && !authPending"
      @retry="query.refresh()"
    >
      <div
        v-if="!collections?.totalCount"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon
          name="i-heroicons-book-open"
          class="size-8 shrink-0 text-gray-500 dark:text-gray-500"
        />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No collections yet</p>
        <p class="collections-hint text-xs text-gray-600 dark:text-gray-400">
          Automatic collections appear as your music library grows.
        </p>
        <UButton
          class="collections-hint"
          :to="location"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="store.isEditing"
          >Browse collections</UButton
        >
      </div>

      <RouterLink
        v-else
        class="collection-collage flex min-h-0 flex-1 flex-col items-center justify-center gap-2 rounded-xl text-center focus-visible:outline-2 focus-visible:outline-primary"
        :to="location"
        :tabindex="store.isEditing ? -1 : undefined"
        :aria-label="`Browse ${collections.totalCount} automatic collections`"
      >
        <div class="collage-art grid min-h-0 w-full flex-1 place-items-center" aria-hidden="true">
          <div class="collage-stack relative aspect-square">
            <div
              v-for="(playlist, index) in collections.items"
              :key="playlist.id"
              class="collage-sleeve absolute aspect-square"
              :style="sleevePositions[index]"
            >
              <HomeCollectionCover :playlist="playlist" />
            </div>
          </div>
        </div>

        <div class="collage-caption min-w-0 shrink-0">
          <p
            class="collection-count font-bold leading-none tabular-nums text-gray-900 dark:text-gray-100"
          >
            {{ collections.totalCount }}
          </p>
          <p class="collection-label mt-2 text-xs text-gray-600 dark:text-gray-400">
            {{ collections.totalCount === 1 ? "collection" : "collections" }}
          </p>
          <p class="collections-hint mt-2 text-xs text-gray-500 dark:text-gray-500">
            Built from your library
          </p>
        </div>
      </RouterLink>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
.collections-widget {
  container-name: collection-body;
  container-type: size;
}

.collage-art {
  container-type: size;
  overflow: hidden;
}

.collage-stack {
  width: min(100cqw, 100cqh);
}

.collage-sleeve {
  width: 48%;
}

.collage-caption {
  width: 100%;
}

.collection-count {
  font-size: clamp(1.5rem, 14cqmin, 4rem);
}

@container collection-body (min-aspect-ratio: 2 / 1) and (min-width: 300px) {
  .collection-collage {
    flex-direction: row;
    text-align: left;
    gap: 1rem;
  }
  .collage-art {
    height: 100%;
    width: 60%;
    flex: none;
  }
  .collage-caption {
    flex: 1;
    width: auto;
  }
}

@container collection-body (max-height: 200px) {
  .collage-caption {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: center;
    column-gap: 0.5rem;
  }

  .collection-label {
    margin-top: 0;
  }

  .collections-hint {
    display: none;
  }
}
</style>
