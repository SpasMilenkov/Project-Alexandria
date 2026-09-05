<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, nextTick, ref, watch } from "vue";

import {
  type EventCalendarRange,
  type OperationalEvent,
  type OperationalEventsQuery,
  parseEventMetadata,
} from "@/api/monitoring";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useServiceDashboardRouting } from "@/composables/useServiceDashboardRouting";
import {
  OperationalEventCode,
  OperationalEventSeverity,
  OperationalEventStatus,
  type ServiceType,
} from "@/enums";
import { useResolveIncident } from "@/mutations/monitoring";
import { operationalEvents } from "@/queries/monitoring";
import { formatDate, formatRelativeShort } from "@/utils/date-formatters";
import {
  EVENT_CODE_ICONS,
  EVENT_CODE_LABELS,
  SEVERITY_BADGE_COLORS,
  SEVERITY_LABELS,
  SERVICE_LABELS,
} from "@/utils/monitoring-display.utils";
import { buildDeepLink } from "@/utils/serviceDashboardRouting";

const { dateRange, initialServiceType, initialSeverity, highlightId, lockedService } = defineProps<{
  dateRange: EventCalendarRange | null;
  /** Deep-link seeds (P6): applied to filter state whenever provided. */
  initialServiceType?: ServiceType | null;
  initialSeverity?: OperationalEventSeverity | null;
  highlightId?: string | null;
  /** Dashboard embedding: pins the feed to initialServiceType and hides the picker. */
  lockedService?: boolean;
}>();
const emit = defineEmits<{ clearDateRange: [] }>();

const PAGE_SIZE = 25;

const page = ref(1);

// undefined = "All" — USelect treats null as an invalid model value
const serviceFilter = ref<ServiceType | undefined>(initialServiceType ?? undefined);
const codeFilter = ref<OperationalEventCode | undefined>(undefined);
const severityFilter = ref<OperationalEventSeverity | undefined>(initialSeverity ?? undefined);
const statusFilter = ref<OperationalEventStatus | undefined>(undefined);

// Later deep-link applications (same-page producers like strip cards) re-seed
// the matching filter.
watch(
  () => initialServiceType,
  (v) => {
    if (v != null) serviceFilter.value = v;
  },
);
watch(
  () => initialSeverity,
  (v) => {
    if (v != null) severityFilter.value = v;
  },
);

// Mobile filter panel — desktop shows filters inline, mobile collapses them
// behind a single toggle so the toolbar never has more than one primary
// control competing for space on a narrow screen.
const filtersOpen = ref(false);

watch([serviceFilter, codeFilter, severityFilter, statusFilter, () => dateRange], () => {
  page.value = 1;
});

const SERVICE_OPTIONS = [
  { label: "All services", value: undefined },
  ...Object.entries(SERVICE_LABELS).map(([value, label]) => ({ label, value: Number(value) })),
];
const CODE_OPTIONS = [
  { label: "All types", value: undefined },
  ...Object.entries(EVENT_CODE_LABELS).map(([value, label]) => ({ label, value: Number(value) })),
];
const SEVERITY_OPTIONS = [
  { label: "All severities", value: undefined },
  ...Object.entries(SEVERITY_LABELS).map(([value, label]) => ({ label, value: Number(value) })),
];
const STATUS_OPTIONS = [
  { label: "All statuses", value: undefined },
  { label: "Active", value: OperationalEventStatus.Active },
  { label: "Resolved", value: OperationalEventStatus.Resolved },
];

const activeFilterCount = computed(
  () =>
    [serviceFilter.value, codeFilter.value, severityFilter.value, statusFilter.value].filter(
      (value) => value !== undefined,
    ).length,
);

const clearFilters = () => {
  serviceFilter.value = undefined;
  codeFilter.value = undefined;
  severityFilter.value = undefined;
  statusFilter.value = undefined;
};

const queryParams = computed<OperationalEventsQuery>(() => ({
  code: codeFilter.value,
  from: dateRange?.from,
  page: page.value,
  pageSize: PAGE_SIZE,
  severity: severityFilter.value,
  serviceType: serviceFilter.value,
  status: statusFilter.value,
  to: dateRange?.to,
}));

const { data, isLoading, error, asyncStatus } = useQuery(
  operationalEvents,
  () => queryParams.value,
);

const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

const visibleError = computed(() =>
  error.value && !isAuthError(error.value) ? error.value : null,
);

const isFetching = computed(() => asyncStatus.value === "loading");

const events = computed<OperationalEvent[]>(() => data.value?.items ?? []);
const totalPages = computed(() => Math.max(data.value?.totalPages ?? 1, 1));

const prevPage = () => {
  if (page.value > 1) page.value--;
};
const nextPage = () => {
  if (data.value?.hasNext && page.value < totalPages.value) page.value++;
};

// Expanded row detail
const expandedId = ref<string | null>(null);

const toggleExpanded = (id: string) => {
  expandedId.value = expandedId.value === id ? null : id;
};

// Deep-link highlight: once data contains the target, ring it and scroll to it
const highlightedId = ref<string | null>(null);

watch(
  [data, () => highlightId],
  ([, target]) => {
    if (!target || !data.value || highlightedId.value === target) return;
    if (!data.value.items.some((event) => event.id === target)) return;

    highlightedId.value = target;
    void nextTick(() => {
      document
        .getElementById(`incident-${target}`)
        ?.scrollIntoView({ behavior: "smooth", block: "center" });
    });
  },
  { immediate: true },
);

// D12 doorway: every incident can open its service dashboard pre-filtered to
// this exact moment.
const { goToServiceDetail } = useServiceDashboardRouting();

const openInDashboard = (event: OperationalEvent) => {
  goToServiceDetail(event.serviceType, buildDeepLink(event));
};

interface MetadataRow {
  label: string;
  value: string;
}

const metadataRows = (event: OperationalEvent): MetadataRow[] => {
  const meta = parseEventMetadata(event.metadataJson);
  if (!meta) return [];
  switch (meta.kind) {
    case "healthcheck-failure":
      return [
        { label: "Check", value: meta.checkName },
        ...(meta.detail ? [{ label: "Detail", value: meta.detail }] : []),
        { label: "Instance", value: meta.serviceInstance },
      ];
    case "error-rate-threshold":
      return [
        {
          label: "Failures",
          value: `${meta.failureCount} in the last ${meta.windowMinutes} minutes`,
        },
        { label: "Threshold", value: `${meta.threshold}% failure rate` },
        { label: "Instance", value: meta.serviceInstance },
      ];
    case "user-reported":
      return [
        { label: "Description", value: meta.description },
        ...(meta.pageContext ? [{ label: "Page", value: meta.pageContext }] : []),
      ];
  }

  // Unrecognized shapes must never crash the row — just render no extra fields
  return [];
};

const formatAbsolute = (iso: string): string =>
  new Date(iso).toLocaleString(undefined, {
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    month: "short",
    year: "numeric",
  });

const ageLabel = (event: OperationalEvent): string => {
  const age = formatRelativeShort(event.createdAt);
  return age === "just now" ? age : `${age} ago`;
};

const resolutionNoteRow = (event: OperationalEvent): string | null =>
  parseEventMetadata(event.metadataJson)?.resolutionNote ?? null;

const subtitle = (event: OperationalEvent): string => {
  const parts = [SERVICE_LABELS[event.serviceType] ?? event.serviceType];
  if (event.resolvedAt) parts.push(`resolved ${formatDate(event.resolvedAt)}`);
  return parts.join(" · ");
};

const isActive = (event: OperationalEvent): boolean =>
  event.status === OperationalEventStatus.Active;

const statusIconBg = (event: OperationalEvent): string =>
  isActive(event) ? "bg-red-500/10" : "bg-gray-500/10";

const statusIconColor = (event: OperationalEvent): string =>
  isActive(event) ? "text-red-500" : "text-gray-400 dark:text-gray-500";

const dateChipLabel = computed(() => {
  if (!dateRange) return "";
  return dateRange.from.toLocaleDateString(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  });
});

// Resolve flow
const toast = useAppToast();
const { mutateAsync: resolveIncident, isLoading: isResolving } = useResolveIncident();

const resolvingEvent = ref<OperationalEvent | null>(null);
const resolveNote = ref("");

const openResolveModal = (event: OperationalEvent) => {
  resolvingEvent.value = event;
  resolveNote.value = "";
};

const handleResolveModalClose = async (confirmed: boolean) => {
  if (!confirmed || !resolvingEvent.value) {
    resolvingEvent.value = null;
    return;
  }
  try {
    await resolveIncident({ id: resolvingEvent.value.id, note: resolveNote.value });
    toast.success("Incident resolved");
    expandedId.value = null;
    resolvingEvent.value = null;
    resolveNote.value = "";
  } catch (err) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 409) {
      toast.warning("Already resolved", "Someone resolved this first — the list is refreshing.");
      resolvingEvent.value = null;
    } else {
      toast.error("Could not resolve incident", err);
    }
  }
};
</script>

<template>
  <div
    class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 frosted-glass overflow-hidden"
  >
    <div
      class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2 flex-wrap"
    >
      <UIcon
        name="i-mdi-format-list-bulleted"
        class="w-4 h-4 text-gray-400 dark:text-gray-500 shrink-0"
      />
      <span class="text-sm font-semibold text-gray-700 dark:text-gray-300">Incident Log</span>

      <!-- Active date filter chip -->
      <UBadge
        v-if="dateRange"
        color="primary"
        variant="soft"
        size="sm"
        class="gap-1 cursor-pointer"
        @click="emit('clearDateRange')"
      >
        {{ dateChipLabel }}
        <UIcon name="i-lucide-x" class="w-3 h-3" />
      </UBadge>

      <!-- Desktop: filters inline -->
      <div class="ml-auto hidden sm:flex items-center gap-2 flex-wrap">
        <USelect
          v-if="!lockedService"
          v-model="serviceFilter"
          :items="SERVICE_OPTIONS"
          placeholder="Service"
          size="xs"
          class="w-40"
        />
        <USelect
          v-model="codeFilter"
          :items="CODE_OPTIONS"
          placeholder="Type"
          size="xs"
          class="w-48"
        />
        <USelect
          v-model="severityFilter"
          :items="SEVERITY_OPTIONS"
          placeholder="Severity"
          size="xs"
          class="w-36"
        />
        <USelect
          v-model="statusFilter"
          :items="STATUS_OPTIONS"
          placeholder="Status"
          size="xs"
          class="w-32"
        />
      </div>

      <!-- Mobile: single toggle, badge shows active count -->
      <UButton
        class="ml-auto sm:hidden"
        color="neutral"
        variant="outline"
        size="xs"
        icon="i-lucide-sliders-horizontal"
        @click="filtersOpen = !filtersOpen"
      >
        Filters
        <UBadge v-if="activeFilterCount" color="primary" variant="solid" size="xs" class="ml-1">
          {{ activeFilterCount }}
        </UBadge>
      </UButton>
    </div>

    <!-- Mobile filter panel -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out overflow-hidden"
      enter-from-class="opacity-0 max-h-0"
      enter-to-class="opacity-100 max-h-96"
      leave-active-class="transition-all duration-150 ease-in overflow-hidden"
      leave-from-class="opacity-100 max-h-96"
      leave-to-class="opacity-0 max-h-0"
    >
      <div
        v-if="filtersOpen"
        class="sm:hidden px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 grid grid-cols-2 gap-2"
      >
        <USelect
          v-if="!lockedService"
          v-model="serviceFilter"
          :items="SERVICE_OPTIONS"
          placeholder="Service"
          size="sm"
          class="w-full col-span-2"
        />
        <USelect
          v-model="codeFilter"
          :items="CODE_OPTIONS"
          placeholder="Type"
          size="sm"
          class="w-full"
        />
        <USelect
          v-model="severityFilter"
          :items="SEVERITY_OPTIONS"
          placeholder="Severity"
          size="sm"
          class="w-full"
        />
        <USelect
          v-model="statusFilter"
          :items="STATUS_OPTIONS"
          placeholder="Status"
          size="sm"
          class="w-full"
        />
        <UButton
          v-if="activeFilterCount"
          color="error"
          variant="outline"
          size="xs"
          class="col-span-2"
          @click="clearFilters"
        >
          Clear filters
        </UButton>
      </div>
    </Transition>

    <!-- Loading: centered spinner, consistent with the other admin sections -->
    <div v-if="isLoading" class="flex items-center justify-center py-14">
      <UIcon name="i-mdi-loading" class="w-6 h-6 text-gray-400 dark:text-gray-500 animate-spin" />
    </div>

    <UAlert
      v-else-if="visibleError"
      color="error"
      variant="subtle"
      icon="i-lucide-alert-circle"
      title="Failed to load incidents"
      :description="visibleError.message"
      class="m-4"
    />

    <!-- Empty state -->
    <div
      v-else-if="!events.length"
      class="flex flex-col items-center justify-center py-14 gap-3 text-center px-5"
    >
      <UIcon name="i-lucide-shield-check" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
      <p class="text-base font-medium text-gray-900 dark:text-gray-100">No incidents found</p>
      <p class="text-sm text-gray-600 dark:text-gray-400 max-w-xs">
        Nothing matches the current filters. Either things are running smoothly or the filters are
        too strict.
      </p>
      <UButton
        v-if="activeFilterCount"
        color="neutral"
        variant="outline"
        size="xs"
        @click="clearFilters"
      >
        Clear filters
      </UButton>
    </div>

    <!-- Event rows -->
    <template v-else>
      <div class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
        <div v-for="event in events" :key="event.id">
          <div
            :id="`incident-${event.id}`"
            role="button"
            tabindex="0"
            class="w-full flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4 px-5 py-4 text-left transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02] cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60 rounded-xl"
            :class="highlightedId === event.id ? 'ring-2 ring-primary/70 bg-primary/5' : ''"
            @click="toggleExpanded(event.id)"
            @keydown.enter.self.prevent="toggleExpanded(event.id)"
            @keydown.space.self.prevent="toggleExpanded(event.id)"
          >
            <div class="flex items-start gap-4">
              <span
                class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                :class="statusIconBg(event)"
              >
                <UIcon
                  :name="EVENT_CODE_ICONS[event.code] ?? 'mdi:help-circle-outline'"
                  class="w-4 h-4"
                  :class="statusIconColor(event)"
                />
              </span>

              <div class="min-w-0 flex-1">
                <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
                  {{ EVENT_CODE_LABELS[event.code] ?? event.code }}
                </p>
                <p class="text-xs text-gray-500 dark:text-gray-400 truncate mt-0.5">
                  {{ subtitle(event) }}
                </p>
              </div>
            </div>

            <!-- Status / severity / time — wraps under the title on mobile,
                 stays on one line on desktop. pl-12 keeps it aligned under
                 the title text (icon width + gap). -->
            <div
              class="flex items-center gap-2 flex-wrap pl-12 sm:pl-0 sm:flex-nowrap sm:gap-3 sm:shrink-0 sm:ml-auto"
            >
              <UBadge :color="isActive(event) ? 'error' : 'neutral'" variant="subtle" size="sm">
                {{ isActive(event) ? "Active" : "Resolved" }}
              </UBadge>
              <UBadge :color="SEVERITY_BADGE_COLORS[event.severity]" variant="subtle" size="sm">
                {{ SEVERITY_LABELS[event.severity] }}
              </UBadge>
              <span class="text-xs text-gray-500 dark:text-gray-500 tabular-nums">
                {{ formatDate(event.createdAt) }}
              </span>
              <UTooltip
                :text="`Open ${SERVICE_LABELS[event.serviceType] ?? event.serviceType} details`"
                :delay-duration="200"
              >
                <UButton
                  icon="i-lucide-arrow-up-right"
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  class="shrink-0"
                  :aria-label="`Open ${SERVICE_LABELS[event.serviceType] ?? event.serviceType} details`"
                  @click.stop="openInDashboard(event)"
                />
              </UTooltip>
              <UIcon
                name="i-lucide-chevron-down"
                class="w-4 h-4 text-gray-400 transition-transform duration-150 ml-auto sm:ml-0"
                :class="expandedId === event.id ? 'rotate-180' : ''"
              />
            </div>
          </div>

          <!-- Expanded detail -->
          <Transition
            enter-active-class="transition-all duration-200 ease-out overflow-hidden"
            enter-from-class="opacity-0 max-h-0"
            enter-to-class="opacity-100 max-h-96"
            leave-active-class="transition-all duration-150 ease-in overflow-hidden"
            leave-from-class="opacity-100 max-h-96"
            leave-to-class="opacity-0 max-h-0"
          >
            <div
              v-if="expandedId === event.id"
              class="px-5 pb-4 pl-5 sm:pl-[4.25rem] bg-black/[0.015] dark:bg-white/[0.015]"
            >
              <dl class="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 py-1">
                <dt
                  class="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400 pt-0.5"
                >
                  Occurred
                </dt>
                <dd class="text-sm text-gray-700 dark:text-gray-300 tabular-nums">
                  {{ formatAbsolute(event.createdAt) }}
                </dd>
                <template v-if="event.resolvedAt">
                  <dt
                    class="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400 pt-0.5"
                  >
                    Resolved
                  </dt>
                  <dd class="text-sm text-gray-700 dark:text-gray-300 tabular-nums">
                    {{ formatAbsolute(event.resolvedAt) }}
                  </dd>
                </template>
                <template v-if="resolutionNoteRow(event)">
                  <dt
                    class="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400 pt-0.5"
                  >
                    Resolution note
                  </dt>
                  <dd class="text-sm text-emerald-600 dark:text-emerald-400 break-words">
                    {{ resolutionNoteRow(event) }}
                  </dd>
                </template>
                <template v-for="row in metadataRows(event)" :key="row.label">
                  <dt
                    class="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400 pt-0.5"
                  >
                    {{ row.label }}
                  </dt>
                  <dd class="text-sm text-gray-700 dark:text-gray-300 break-words">
                    {{ row.value }}
                  </dd>
                </template>
              </dl>

              <UButton
                v-if="isActive(event)"
                size="xs"
                variant="outline"
                color="primary"
                icon="i-lucide-check-circle"
                class="mt-3"
                :disabled="isResolving"
                @click.stop="openResolveModal(event)"
              >
                Mark resolved
              </UButton>
            </div>
          </Transition>
        </div>
      </div>

      <!-- Pagination footer -->
      <div
        class="px-5 py-3 border-t border-gray-200/70 dark:border-gray-700/70 flex items-center justify-between gap-3"
      >
        <span class="text-xs text-gray-500 dark:text-gray-400 tabular-nums">
          Page {{ data?.currentPage ?? page }} of {{ totalPages }} ·
          {{ (data?.totalCount ?? 0).toLocaleString() }} events
        </span>
        <div class="flex items-center gap-2">
          <UButton
            icon="i-lucide-chevron-left"
            color="neutral"
            variant="outline"
            size="xs"
            :disabled="page <= 1 || isFetching"
            aria-label="Previous page"
            @click="prevPage"
          />
          <UButton
            icon="i-lucide-chevron-right"
            color="neutral"
            variant="outline"
            size="xs"
            :disabled="!data?.hasNext || isFetching"
            aria-label="Next page"
            @click="nextPage"
          />
        </div>
      </div>
    </template>

    <!-- Resolve confirmation -->
    <ConfirmModal
      :open="!!resolvingEvent"
      title="Resolve incident?"
      description="This marks the incident as manually resolved."
      confirm-label="Mark resolved"
      confirm-icon="i-lucide-check-circle"
      confirm-color="primary"
      :loading="isResolving"
      :alert="{
        title: 'What happens next',
        description: 'The health strip returns to Healthy and uptime windows are recalculated.',
        icon: 'i-lucide-info',
        color: 'info',
      }"
      @close="handleResolveModalClose"
    >
      <div v-if="resolvingEvent" class="space-y-3">
        <div class="flex items-center gap-3">
          <span class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 bg-gray-500/10">
            <UIcon
              :name="EVENT_CODE_ICONS[resolvingEvent.code] ?? 'mdi:help-circle-outline'"
              class="w-4 h-4 text-gray-500 dark:text-gray-400"
            />
          </span>
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate min-w-0 flex-1">
            {{ EVENT_CODE_LABELS[resolvingEvent.code] ?? resolvingEvent.code }}
          </p>
          <UBadge
            :color="SEVERITY_BADGE_COLORS[resolvingEvent.severity]"
            variant="subtle"
            size="sm"
            class="shrink-0"
          >
            {{ SEVERITY_LABELS[resolvingEvent.severity] }}
          </UBadge>
        </div>

        <div
          class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 px-3 py-2 flex items-center gap-2"
        >
          <UIcon
            name="i-mdi-clock-outline"
            class="w-3.5 h-3.5 text-gray-400 dark:text-gray-500 shrink-0"
          />
          <span class="text-xs text-gray-600 dark:text-gray-300 tabular-nums truncate">
            {{ SERVICE_LABELS[resolvingEvent.serviceType] ?? resolvingEvent.serviceType }} ·
            occurred {{ formatAbsolute(resolvingEvent.createdAt) }} ({{ ageLabel(resolvingEvent) }})
          </span>
        </div>

        <UFormField label="Resolution note (optional)" hint="Max 1000 characters">
          <UTextarea
            v-model="resolveNote"
            class="w-full"
            :rows="3"
            :maxlength="1000"
            placeholder="What fixed it?"
            autoresize
          />
        </UFormField>
      </div>
    </ConfirmModal>
  </div>
</template>
