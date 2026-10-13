<script setup lang="ts">
import { useEventListener, useIntervalFn, useNow } from "@vueuse/core";
import { computed, ref } from "vue";

import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { useHomeWidgetRows } from "@/composables/useHomeWidgetRows";
import { HomeActivityStyle } from "@/enums/home-widget";
import { homeRecentActivity } from "@/queries/home-status-activity";
import { useHomeStore } from "@/stores/home";
import {
  homeActivityDescription,
  homeActivityDisplayRows,
  homeActivityGroups,
  homeActivityPresentation,
  homeActivityTime,
} from "@/utils/home-status-activity";

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const now = useNow({ interval: 60_000 });
const list = ref<HTMLElement | null>(null);
const isByDay = computed(() => widget.options.activityStyle === HomeActivityStyle.ByDay);

const query = useHomeWidgetQuery(
  () => homeRecentActivity({ userId: store.ownerId ?? "", epoch: store.ownerEpoch }),
  (preview) => ({ userId: store.ownerId ?? "", epoch: store.ownerEpoch, value: preview.activity }),
);

const events = computed(() => {
  const response = query.data.value;
  if (
    response?.userId !== store.ownerId ||
    response.epoch !== store.ownerEpoch ||
    query.error.value
  )
    return null;

  return response.value.items;
});

const authPending = computed(() => isAuthError(query.error.value));
const rowCount = useHomeWidgetRows(list, () => (isByDay.value ? 8 : 48));
const displayRows = computed(() => {
  if (isByDay.value)
    return homeActivityDisplayRows(
      homeActivityGroups(events.value ?? [], now.value),
      rowCount.value * 8,
    );

  return (events.value ?? [])
    .slice(0, rowCount.value)
    .map((event, index) => ({ kind: "event" as const, event, key: String(index) }));
});

const refresh = () => {
  if (store.ownerId && !store.isEditing && !document.hidden) void query.refresh();
};

useIntervalFn(refresh, 60_000);
useEventListener(document, "visibilitychange", refresh);
</script>

<template>
  <div
    class="activity-widget flex min-h-0 flex-1 flex-col gap-2"
    :class="{ 'pointer-events-none': store.isEditing }"
    :data-activity-style="widget.options.activityStyle"
  >
    <HomeWidgetDataState
      :loading="authPending || (!events && !query.error.value)"
      :failed="!events && Boolean(query.error.value)"
      @retry="query.refresh()"
    >
      <div
        v-if="events && !events.length"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon name="i-lucide-activity" class="size-8 text-gray-500 dark:text-gray-500" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No recent activity</p>
        <p class="activity-description text-xs text-gray-600 dark:text-gray-400">
          Your library and account activity will appear here.
        </p>
      </div>

      <template v-else-if="events">
        <div
          ref="list"
          class="min-h-0 flex-1 overflow-hidden"
          role="list"
          aria-label="Recent activity"
        >
          <template v-for="row in displayRows" :key="row.key">
            <p
              v-if="row.kind === 'heading'"
              class="activity-day flex items-center text-xs font-medium text-gray-600 dark:text-gray-400"
              role="presentation"
            >
              {{ row.label }}
            </p>

            <div
              v-else
              class="activity-row flex items-center gap-2 border-b border-gray-200/70 last:border-0 dark:border-gray-700/70"
              role="listitem"
            >
              <UIcon
                :name="homeActivityPresentation(row.event.operationType).icon"
                class="size-4 shrink-0"
                :class="homeActivityPresentation(row.event.operationType).text"
              />

              <p
                class="min-w-0 flex-1 truncate text-xs text-gray-900 dark:text-gray-100"
                :title="homeActivityDescription(row.event)"
              >
                {{ homeActivityDescription(row.event) }}
              </p>

              <UBadge
                v-if="!isByDay"
                :color="homeActivityPresentation(row.event.operationType).color"
                variant="subtle"
                size="xs"
                class="activity-badge shrink-0"
                >{{ homeActivityPresentation(row.event.operationType).label }}</UBadge
              >

              <time
                :datetime="row.event.timestamp"
                :title="row.event.timestamp"
                class="activity-time shrink-0 text-right text-xs text-gray-500 dark:text-gray-500"
                >{{ homeActivityTime(row.event.timestamp, now) }}</time
              >
            </div>
          </template>
        </div>

        <RouterLink
          to="/access-history"
          :tabindex="store.isEditing ? -1 : undefined"
          class="activity-link self-start text-xs text-gray-600 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400"
          >View activity history</RouterLink
        >
      </template>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
.activity-row {
  height: 3rem;
}

.activity-widget[data-activity-style="1"] .activity-row {
  height: 2.5rem;
}

.activity-day {
  height: 1.5rem;
}

.activity-badge,
.activity-link {
  display: none;
}

@container home-widget (min-width: 380px) {
  .activity-badge {
    display: inline-flex;
  }
}

@container home-widget (min-height: 240px) {
  .activity-link {
    display: block;
  }
}

@container home-widget (max-height: 200px) {
  .activity-description {
    display: none;
  }
}

@container home-widget (max-width: 200px) {
  .activity-row {
    gap: 0.25rem;
  }

  .activity-time {
    font-size: 0.625rem;
  }
}
</style>
