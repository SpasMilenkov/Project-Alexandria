<script setup lang="ts">
import { useEventListener, useIntervalFn, useNow } from "@vueuse/core";
import { computed, ref } from "vue";

import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { useHomeWidgetRows } from "@/composables/useHomeWidgetRows";
import { HomeStatusStyle } from "@/enums/home-widget";
import { homeSystemStatus } from "@/queries/home-status-activity";
import { useHomeStore } from "@/stores/home";
import {
  HOME_STATUS_PRESENTATION,
  homeCurrentStatusRows,
  homeHistoryRows,
  homeOverallStatus,
} from "@/utils/home-status-activity";

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const now = useNow({ interval: 60_000 });
const list = ref<HTMLElement | null>(null);
const isHistory = computed(() => widget.options.statusStyle === HomeStatusStyle.History);
const badgeOnly = computed(
  () =>
    widget.options.statusStyle === HomeStatusStyle.Beacon &&
    widget.width <= 2 &&
    widget.height <= 2,
);

const query = useHomeWidgetQuery(
  () =>
    homeSystemStatus({
      userId: store.ownerId ?? "",
      epoch: store.ownerEpoch,
      history: isHistory.value,
    }),
  (preview) => {
    const session = {
      userId: store.ownerId ?? "",
      epoch: store.ownerEpoch,
      history: isHistory.value,
    };

    if (isHistory.value) return { ...session, current: null, historyDays: preview.statusHistory };

    return { ...session, current: preview.currentStatus, historyDays: null };
  },
);

const response = computed(() => {
  const data = query.data.value;
  if (
    data?.userId !== store.ownerId ||
    data.epoch !== store.ownerEpoch ||
    data.history !== isHistory.value ||
    query.error.value
  )
    return null;

  return data;
});

const currentRows = computed(() => homeCurrentStatusRows(response.value?.current ?? []));
const historyRows = computed(() => homeHistoryRows(response.value?.historyDays ?? [], now.value));
const verdict = computed(
  () => HOME_STATUS_PRESENTATION[homeOverallStatus(currentRows.value.map((row) => row.state))],
);
const authPending = computed(() => isAuthError(query.error.value));
const empty = computed(() => {
  if (!response.value) return false;
  if (isHistory.value) return !response.value.historyDays?.length;

  return !response.value.current?.length;
});

const rowCount = useHomeWidgetRows(list, () => (isHistory.value ? 56 : 32));

const refresh = () => {
  if (store.ownerId && !store.isEditing && !document.hidden) void query.refresh();
};

useIntervalFn(refresh, 60_000);
useEventListener(document, "visibilitychange", refresh);
</script>

<template>
  <div
    class="status-widget flex min-h-0 flex-1 flex-col gap-2"
    :data-status-style="widget.options.statusStyle"
    :class="[
      { 'pointer-events-none': store.isEditing, 'status-badge-only': badgeOnly },
      badgeOnly && response && !empty ? verdict.wash : undefined,
    ]"
  >
    <p
      v-if="query.error.value && !authPending"
      class="text-sm font-medium text-gray-900 dark:text-gray-100"
    >
      Status unknown
    </p>

    <HomeWidgetDataState
      :loading="authPending || (!response && !query.error.value)"
      :failed="!response && Boolean(query.error.value)"
      @retry="query.refresh()"
    >
      <div
        v-if="empty"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon name="i-lucide-circle" class="size-8 text-gray-500 dark:text-gray-500" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No status data</p>
        <p class="status-description text-xs text-gray-600 dark:text-gray-400">
          Service information is not available yet.
        </p>
      </div>

      <template v-else-if="response">
        <div
          v-if="!isHistory"
          class="status-verdict flex shrink-0 items-center gap-2"
          :class="{ 'status-beacon': widget.options.statusStyle === HomeStatusStyle.Beacon }"
        >
          <span
            v-if="widget.options.statusStyle === HomeStatusStyle.Beacon"
            class="status-symbol flex shrink-0 items-center justify-center rounded-2xl"
            :class="[badgeOnly ? undefined : verdict.wash, verdict.text]"
          >
            <UIcon :name="verdict.icon" class="size-8" />
          </span>
          <span
            v-else
            class="size-2 shrink-0 rounded-full"
            :class="
              HOME_STATUS_PRESENTATION[homeOverallStatus(currentRows.map((row) => row.state))].fill
            "
          />

          <div class="min-w-0">
            <p class="status-verdict-label font-semibold text-gray-900 dark:text-gray-100">
              {{ verdict.verdict }}
            </p>
            <p
              v-if="!badgeOnly"
              class="status-description mt-2 text-xs text-gray-600 dark:text-gray-400"
            >
              Recorded incidents across {{ currentRows.length }} services
            </p>
          </div>
        </div>

        <p v-else class="shrink-0 text-xs text-gray-600 dark:text-gray-400">Last 30 days · UTC</p>

        <div
          v-if="!badgeOnly"
          ref="list"
          class="status-service-list min-h-0 flex-1 overflow-hidden"
        >
          <template v-if="isHistory">
            <div
              v-for="row in historyRows.slice(0, rowCount)"
              :key="row.service"
              class="status-history-row flex flex-col justify-center gap-2"
            >
              <p
                class="truncate text-xs font-medium text-gray-900 dark:text-gray-100"
                :title="row.label"
              >
                {{ row.label }}
              </p>

              <div
                class="status-strip flex min-w-0 gap-0.5"
                :aria-label="`${row.label}: daily recorded status, oldest to newest`"
              >
                <span
                  v-for="day in row.days"
                  :key="day.date"
                  class="h-4 min-w-0 flex-1 rounded-sm"
                  :class="HOME_STATUS_PRESENTATION[day.state].fill"
                  :title="`${day.date}: ${HOME_STATUS_PRESENTATION[day.state].label}`"
                  :aria-label="`${day.date}: ${HOME_STATUS_PRESENTATION[day.state].label}`"
                  role="img"
                />
              </div>
            </div>
          </template>

          <template v-else>
            <div
              v-for="row in currentRows.slice(0, rowCount)"
              :key="row.service"
              class="status-service-row flex items-center gap-2 border-b border-gray-200/70 last:border-0 dark:border-gray-700/70"
            >
              <span
                class="size-2 shrink-0 rounded-full"
                :class="HOME_STATUS_PRESENTATION[row.state].fill"
              />
              <span
                class="min-w-0 flex-1 truncate text-xs text-gray-900 dark:text-gray-100"
                :title="row.label"
                >{{ row.label }}</span
              >
              <span class="shrink-0 text-xs" :class="HOME_STATUS_PRESENTATION[row.state].text">{{
                HOME_STATUS_PRESENTATION[row.state].label
              }}</span>
            </div>
          </template>
        </div>

        <RouterLink
          v-if="!badgeOnly"
          to="/status"
          :tabindex="store.isEditing ? -1 : undefined"
          class="status-link self-start text-xs text-gray-600 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400"
          >View status page</RouterLink
        >
      </template>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
.status-verdict-label {
  font-size: 0.875rem;
}

.status-beacon .status-verdict-label {
  font-size: clamp(0.875rem, 6cqi, 1.5rem);
  line-height: 1.2;
}

.status-symbol {
  width: 3.5rem;
  height: 3.5rem;
}

.status-badge-only {
  container-type: size;
  border-radius: 0.75rem;
}

.status-badge-only .status-verdict {
  flex: 1;
  flex-direction: column;
  justify-content: center;
  text-align: center;
  font-size: clamp(0.875rem, 14cqh, 1.125rem);
}

.status-badge-only .status-verdict-label {
  font-size: inherit;
}

.status-badge-only .status-symbol {
  width: min(60cqw, calc(100cqh - 2.4em - 0.5rem));
  height: min(60cqw, calc(100cqh - 2.4em - 0.5rem));
}

.status-badge-only .status-symbol :deep(.iconify) {
  width: 100%;
  height: 100%;
}

.status-service-row {
  height: 2rem;
}

.status-history-row {
  height: 3.5rem;
}

.status-link {
  display: none;
}

@container home-widget (min-height: 240px) {
  .status-link {
    display: block;
  }
}

@container home-widget (max-height: 200px) {
  .status-description {
    display: none;
  }

  .status-symbol {
    width: 2.5rem;
    height: 2.5rem;
  }

  .status-symbol :deep(.iconify) {
    width: 1.5rem;
    height: 1.5rem;
  }
}

@container home-widget (min-width: 420px) and (max-height: 160px) {
  .status-widget[data-status-style="0"]:not(.status-badge-only) {
    flex-direction: row;
    align-items: center;
    gap: 1rem;
  }

  .status-widget[data-status-style="0"]:not(.status-badge-only) .status-verdict {
    flex-basis: 50%;
  }

  .status-widget[data-status-style="0"]:not(.status-badge-only) .status-service-list {
    align-self: stretch;
  }
}
</style>
