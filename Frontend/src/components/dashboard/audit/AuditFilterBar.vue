<script setup lang="ts">
import { CalendarDate } from "@internationalized/date";
import { useQuery } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import DateRangeFilter, { type DateRangeValue } from "@/components/common/DateRangeFilter.vue";
import InfoHint from "@/components/common/InfoHint.vue";
import { getAuditMessage } from "@/composables/useAuditMessage";
import { AuditEventCode } from "@/enums/audit-event-code";
import { EntityType } from "@/enums/entity-type";
import { LogSource } from "@/enums/log-source";
import { OperationType } from "@/enums/operation-type";
import {
  ENTITY_TYPE_OPTIONS,
  OPERATION_OPTIONS,
  SOURCE_DESCRIPTIONS,
  SOURCE_OPTIONS,
  SOURCE_SUMMARIES,
  entityIcon,
  entityLabel,
  operationIcon,
  operationLabel,
  sourceIcon,
} from "@/features/audit/presentation";
import { type AuditFilterField, type AuditFilters, hasFilterValue } from "@/features/audit/query";
import { auditUserOptions } from "@/queries/adminAudit";
import { formatCalendarDate, formatCalendarDateRange } from "@/utils/date-formatters";

const {
  fields,
  controls = undefined,
  entityTypeOptions = ENTITY_TYPE_OPTIONS,
  operationOptions = OPERATION_OPTIONS,
  eventCodes = undefined,
} = defineProps<{
  // Which filters this page honours; others are never shown or counted.
  fields: AuditFilterField[];
  // Which of those get a control in the panel. Defaults to all of them; the rest only
  // arrive through deep links and show as removable pills.
  controls?: AuditFilterField[];
  // The choices a picker offers, narrowed where the reader can only see part of the log.
  entityTypeOptions?: { icon: string; label: string; value: EntityType }[];
  operationOptions?: { icon: string; label: string; value: OperationType }[];
  eventCodes?: AuditEventCode[];
}>();

const filters = defineModel<AuditFilters>({ required: true });

const offers = (field: AuditFilterField) => fields.includes(field);

const hasControl = (field: AuditFilterField) =>
  offers(field) && (controls ?? fields).includes(field);

const isPanelOpen = ref(false);

const update = (patch: Partial<AuditFilters>) => {
  filters.value = { ...filters.value, ...patch, page: 1 };
};

// The user list only loads where the actor and owner pickers exist.
const { data: users } = useQuery(() => ({
  ...auditUserOptions(),
  enabled: offers("user") || offers("owner"),
}));

const userItems = computed(() =>
  (users.value?.items ?? []).map((user) => ({
    description: user.email,
    label: user.userName,
    value: user.id,
  })),
);

const userName = (id: string | undefined) => {
  if (!id) return "";

  return userItems.value.find((item) => item.value === id)?.label ?? `${id.slice(0, 8)}…`;
};

const ALL = -1;

const fieldUi = { base: "h-10 rounded-xl" };

const entityTypeItems = computed(() => [
  { icon: "i-lucide-layout-grid", label: "Any type", value: ALL },
  ...entityTypeOptions,
]);
const operationItems = computed(() => [
  { icon: "i-lucide-activity", label: "Any action", value: ALL },
  ...operationOptions,
]);
const sourceItems = [
  { icon: "i-lucide-history", label: "Any source", value: ALL },
  ...SOURCE_OPTIONS.map((option) => ({
    ...option,
    description: SOURCE_SUMMARIES[option.value],
  })),
];

// The closed select shows the chosen option's icon, so a set filter is readable at a glance.
const selectedIcon = (items: { icon: string; value: number }[], value: number) =>
  items.find((item) => item.value === value)?.icon;

const allEventCodes = Object.values(AuditEventCode)
  .filter((value): value is AuditEventCode => typeof value === "number")
  .filter((code) => code !== AuditEventCode.Unknown);

const eventItems = computed<{ label: string; value: AuditEventCode }[]>(() =>
  (eventCodes ?? allEventCodes)
    .map((code) => ({ label: getAuditMessage(code), value: code }))
    .sort((a, b) => a.label.localeCompare(b.label)),
);

const fromSelect = <T extends number>(value: number): T | undefined => {
  if (value === ALL) return undefined;

  return value as T;
};

const entityTypeModel = computed({
  get: () => filters.value.entityType ?? ALL,
  set: (value: number) => update({ entityType: fromSelect<EntityType>(value) }),
});

const operationModel = computed({
  get: () => filters.value.operation ?? ALL,
  set: (value: number) => update({ operation: fromSelect<OperationType>(value) }),
});

const sourceModel = computed({
  get: () => filters.value.source ?? ALL,
  set: (value: number) => update({ source: fromSelect<LogSource>(value) }),
});

const eventsModel = computed({
  get: () => filters.value.events,
  set: (value: AuditEventCode[]) => update({ events: [...value].sort((a, b) => a - b) }),
});

const userModel = computed({
  get: () => filters.value.user,
  set: (value: string | undefined) => update({ user: value || undefined }),
});

const ownerModel = computed({
  get: () => filters.value.owner,
  set: (value: string | undefined) => update({ owner: value || undefined }),
});

// Days are UTC days, the same buckets the activity calendar counts in.
const toCalendarDay = (iso: string | undefined) => {
  if (!iso) return undefined;

  const date = new Date(iso);

  return new CalendarDate(date.getUTCFullYear(), date.getUTCMonth() + 1, date.getUTCDate());
};

const dateRangeModel = computed<DateRangeValue>({
  get: () => ({ end: toCalendarDay(filters.value.to), start: toCalendarDay(filters.value.from) }),
  set: (range: DateRangeValue) => {
    if (!range.start || !range.end) {
      update({ from: undefined, to: undefined });
      return;
    }

    const from = Date.UTC(range.start.year, range.start.month - 1, range.start.day);
    const to = Date.UTC(range.end.year, range.end.month - 1, range.end.day + 1) - 1;

    update({ from: new Date(from).toISOString(), to: new Date(to).toISOString() });
  },
});

// Free-text fields commit after typing settles, so each keystroke is not a request.
const ipDraft = ref(filters.value.ip ?? "");
const entityIdDraft = ref(filters.value.entityId ?? "");
const correlationDraft = ref(filters.value.correlationId ?? "");

watch(
  () => filters.value,
  (next) => {
    ipDraft.value = next.ip ?? "";
    entityIdDraft.value = next.entityId ?? "";
    correlationDraft.value = next.correlationId ?? "";
  },
);

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;

const isGuidOrEmpty = (value: string) => !value.trim() || GUID.test(value.trim());

// An incomplete ID keeps the current filter until it is a full one.
const draftId = (draft: string, current: string | undefined) => {
  if (!isGuidOrEmpty(draft)) return current;

  return draft.trim().toLowerCase() || undefined;
};

const commitText = useDebounceFn(() => {
  const current = filters.value;
  const ip = ipDraft.value.trim() || undefined;
  const entityId = draftId(entityIdDraft.value, current.entityId);
  const correlationId = draftId(correlationDraft.value, current.correlationId);

  const unchanged =
    ip === current.ip && entityId === current.entityId && correlationId === current.correlationId;
  if (unchanged) return;

  update({ correlationId, entityId, ip });
}, 600);

const dateLabel = computed(() => {
  const start = toCalendarDay(filters.value.from);
  const end = toCalendarDay(filters.value.to);
  if (!start) return "";

  const startDate = new Date(Date.UTC(start.year, start.month - 1, start.day));
  if (!end) return `From ${formatCalendarDate(startDate, { timeZone: "UTC" })}`;

  const endDate = new Date(Date.UTC(end.year, end.month - 1, end.day));
  if (start.compare(end) === 0) return formatCalendarDate(startDate, { timeZone: "UTC" });

  return formatCalendarDateRange(startDate, endDate, { timeZone: "UTC" });
});

const eventsLabel = computed(() => {
  const [first, ...rest] = filters.value.events;
  if (first === undefined) return "";
  if (!rest.length) return getAuditMessage(first);

  return `${getAuditMessage(first)} and ${rest.length} more`;
});

interface FilterPill {
  key: string;
  label: string;
  icon?: string;
  clear: () => void;
}

const pills = computed<FilterPill[]>(() => {
  const current = filters.value;
  const list: FilterPill[] = [];
  const add = (field: AuditFilterField, label: string, clear: () => void, icon?: string) => {
    if (offers(field) && hasFilterValue(current, field)) {
      list.push({ clear, icon, key: field, label });
    }
  };

  add("user", `Done by ${userName(current.user)}`, () => update({ user: undefined }));
  add("owner", `Affecting ${userName(current.owner)}`, () => update({ owner: undefined }));
  add(
    "entityType",
    entityLabel(current.entityType ?? EntityType.File),
    () => update({ entityType: undefined }),
    entityIcon(current.entityType ?? EntityType.File),
  );
  add("entityId", "One item", () => update({ entityId: undefined }));
  add("events", eventsLabel.value, () => update({ events: [] }));
  add(
    "operation",
    operationLabel(current.operation ?? OperationType.Read),
    () => update({ operation: undefined }),
    operationIcon(current.operation ?? OperationType.Read),
  );
  add(
    "source",
    SOURCE_OPTIONS.find((option) => option.value === current.source)?.label ?? "Source",
    () => update({ source: undefined }),
    sourceIcon(current.source ?? LogSource.API),
  );
  add("ip", `IP ${current.ip ?? ""}`, () => update({ ip: undefined }));

  if (offers("from") && (current.from || current.to)) {
    list.push({
      clear: () => update({ from: undefined, to: undefined }),
      key: "dates",
      label: dateLabel.value,
    });
  }

  add("correlationId", "One request", () => update({ correlationId: undefined }));

  return list;
});

// Deep links can carry filters a page has no control for, such as an item id; they still
// show as pills so they can be cleared.
const activeCount = computed(() => pills.value.length);

const isFilterButtonActive = computed(() => isPanelOpen.value || activeCount.value > 0);

const filterButtonColor = computed(() => {
  if (isFilterButtonActive.value) return "primary";

  return "neutral";
});

const filterButtonVariant = computed(() => {
  if (isFilterButtonActive.value) return "soft";

  return "outline";
});

// Rows in the panel, so a dozen controls read as four questions instead of one flat grid.
const FILTER_GROUPS = [
  {
    fields: ["from"],
    hint: "Whole days, in UTC",
    icon: "i-lucide-calendar-range",
    key: "when",
    label: "When",
  },
  {
    fields: ["user", "owner"],
    hint: "Who acted, and whose data it was",
    icon: "i-lucide-users",
    key: "people",
    label: "People",
  },
  {
    fields: ["entityType", "operation", "events"],
    hint: "The item and the kind of change",
    icon: "i-lucide-list",
    key: "what",
    label: "What happened",
  },
  {
    fields: ["source", "ip", "correlationId", "entityId"],
    hint: "Where a change came from",
    icon: "i-lucide-fingerprint",
    key: "origin",
    label: "Origin",
  },
] satisfies {
  fields: AuditFilterField[];
  hint: string;
  icon: string;
  key: string;
  label: string;
}[];

type FilterGroup = (typeof FILTER_GROUPS)[number];

const visibleGroups = computed(() =>
  FILTER_GROUPS.filter((group) => group.fields.some((field) => hasControl(field))),
);

// A date range is one filter even though it spans two fields.
const isFieldActive = (field: AuditFilterField) => {
  if (field === "from") {
    return hasFilterValue(filters.value, "from") || hasFilterValue(filters.value, "to");
  }

  return hasFilterValue(filters.value, field);
};

const groupActiveCount = (group: FilterGroup) =>
  group.fields.filter((field) => hasControl(field) && isFieldActive(field)).length;

const appliedSummary = computed(() => {
  if (activeCount.value === 0) return "No filters applied";
  if (activeCount.value === 1) return "1 filter applied";

  return `${activeCount.value} filters applied`;
});

const clearAll = () => {
  const cleared: Partial<AuditFilters> = {};

  for (const field of fields) {
    if (field === "events") {
      cleared.events = [];
    } else {
      cleared[field] = undefined;
    }
  }

  update(cleared);
};
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="flex flex-wrap items-center gap-2">
      <UButton
        icon="i-lucide-sliders-horizontal"
        label="Filters"
        :color="filterButtonColor"
        :variant="filterButtonVariant"
        size="sm"
        :aria-expanded="isPanelOpen"
        aria-controls="audit-filter-panel"
        class="shrink-0 rounded-lg"
        @click="isPanelOpen = !isPanelOpen"
      >
        <template v-if="activeCount" #trailing>
          <span
            class="flex size-5 items-center justify-center rounded-full bg-primary text-xs font-semibold text-inverted tabular-nums"
          >
            {{ activeCount }}
          </span>
        </template>
      </UButton>

      <span
        v-if="pills.length"
        aria-hidden="true"
        class="hidden h-6 w-px bg-gray-200/70 sm:block dark:bg-gray-700/70"
      />

      <span
        v-for="pill in pills"
        :key="pill.key"
        class="inline-flex max-w-full items-center gap-1 rounded-lg bg-primary/10 py-0.5 pr-0.5 pl-2.5 text-xs font-semibold text-primary"
      >
        <UIcon v-if="pill.icon" :name="pill.icon" class="h-3.5 w-3.5 shrink-0" />
        <span class="truncate">{{ pill.label }}</span>
        <UButton
          icon="i-lucide-x"
          color="primary"
          variant="ghost"
          size="xs"
          class="justify-center rounded-md"
          :aria-label="`Remove filter: ${pill.label}`"
          @click="pill.clear()"
        />
      </span>

      <UButton
        v-if="pills.length > 1"
        label="Clear all"
        color="neutral"
        variant="ghost"
        size="xs"
        @click="clearAll"
      />

      <div class="ms-auto flex items-center gap-2">
        <slot name="actions" />
      </div>
    </div>

    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition-all duration-150 ease-in"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <div
        v-if="isPanelOpen"
        id="audit-filter-panel"
        class="rounded-xl border border-gray-200/70 bg-gray-500/5 dark:border-gray-700/70 dark:bg-gray-400/5"
      >
        <div class="divide-y divide-gray-200/70 px-4 md:px-5 dark:divide-gray-700/70">
          <div
            v-for="group in visibleGroups"
            :key="group.key"
            role="group"
            :aria-label="group.label"
            class="flex min-w-0 flex-col gap-3 py-4 md:flex-row md:gap-6"
          >
            <div class="flex shrink-0 items-start gap-2.5 md:w-48" aria-hidden="true">
              <span
                class="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-lg bg-gray-500/10 dark:bg-gray-400/10"
              >
                <UIcon :name="group.icon" class="h-4 w-4 text-gray-600 dark:text-gray-400" />
              </span>

              <div class="min-w-0">
                <p
                  class="flex items-center gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
                >
                  {{ group.label }}
                  <span
                    v-if="groupActiveCount(group)"
                    class="flex size-5 items-center justify-center rounded-full bg-primary text-xs font-semibold text-inverted tabular-nums"
                  >
                    {{ groupActiveCount(group) }}
                  </span>
                </p>
                <p class="text-xs text-gray-600 dark:text-gray-400">{{ group.hint }}</p>
              </div>
            </div>

            <div
              class="grid min-w-0 flex-1 grid-cols-1 gap-3 sm:grid-cols-2 xl:max-w-5xl xl:grid-cols-3"
            >
              <template v-if="group.key === 'when'">
                <UFormField label="Date range">
                  <DateRangeFilter
                    v-model="dateRangeModel"
                    label="Dates"
                    icon="i-lucide-calendar-range"
                  />
                </UFormField>
              </template>

              <template v-else-if="group.key === 'people'">
                <UFormField v-if="hasControl('user')" label="Done by">
                  <USelectMenu
                    v-model="userModel"
                    :items="userItems"
                    value-key="value"
                    placeholder="Anyone"
                    icon="i-lucide-user"
                    :search-input="{ placeholder: 'Search accounts…' }"
                    clear
                    :ui="fieldUi"
                    class="w-full"
                  />
                </UFormField>

                <UFormField v-if="hasControl('owner')" label="Affecting">
                  <USelectMenu
                    v-model="ownerModel"
                    :items="userItems"
                    value-key="value"
                    placeholder="Any account"
                    icon="i-lucide-user-round"
                    :search-input="{ placeholder: 'Search accounts…' }"
                    clear
                    :ui="fieldUi"
                    class="w-full"
                  />
                </UFormField>
              </template>

              <template v-else-if="group.key === 'what'">
                <UFormField v-if="hasControl('entityType')" label="Item type">
                  <USelect
                    v-model="entityTypeModel"
                    :items="entityTypeItems"
                    :icon="selectedIcon(entityTypeItems, entityTypeModel)"
                    :ui="fieldUi"
                    class="w-full"
                  />
                </UFormField>

                <UFormField v-if="hasControl('operation')" label="Action">
                  <USelect
                    v-model="operationModel"
                    :items="operationItems"
                    :icon="selectedIcon(operationItems, operationModel)"
                    :ui="fieldUi"
                    class="w-full"
                  />
                </UFormField>

                <UFormField v-if="hasControl('events')" label="Events">
                  <USelectMenu
                    v-model="eventsModel"
                    :items="eventItems"
                    value-key="value"
                    multiple
                    placeholder="Any event"
                    :search-input="{ placeholder: 'Search events…' }"
                    :ui="fieldUi"
                    class="w-full"
                  />
                </UFormField>
              </template>

              <template v-else>
                <UFormField v-if="hasControl('source')" label="Recorded by">
                  <template #hint>
                    <InfoHint
                      title="Where entries come from"
                      icon="i-lucide-history"
                      wide
                      guidance="One bulk move shows as an app summary plus a database entry per file. The timeline folds them into one row."
                    >
                      Three recorders write to this log, each seeing a change from its own angle.

                      <template #detail>
                        <ul
                          class="grid grid-cols-1 gap-1 rounded-xl bg-gray-500/10 p-1.5 sm:grid-cols-3"
                        >
                          <li
                            v-for="option in SOURCE_OPTIONS"
                            :key="option.value"
                            class="flex flex-col gap-1.5 rounded-lg px-2.5 py-2"
                          >
                            <span class="flex items-center gap-2">
                              <UIcon
                                :name="option.icon"
                                class="size-4 shrink-0 text-gray-600 dark:text-gray-400"
                                aria-hidden="true"
                              />
                              <span class="text-xs font-semibold text-gray-900 dark:text-gray-100">
                                {{ option.label }}
                              </span>
                            </span>
                            <span class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
                              {{ SOURCE_DESCRIPTIONS[option.value] }}
                            </span>
                          </li>
                        </ul>
                      </template>
                    </InfoHint>
                  </template>

                  <USelect
                    v-model="sourceModel"
                    :items="sourceItems"
                    :icon="selectedIcon(sourceItems, sourceModel)"
                    :ui="fieldUi"
                    class="w-full"
                  />
                </UFormField>

                <UFormField v-if="hasControl('ip')" label="IP address">
                  <UInput
                    v-model="ipDraft"
                    placeholder="192.168.1.20"
                    :ui="fieldUi"
                    class="w-full"
                    @update:model-value="commitText"
                  />
                </UFormField>

                <UFormField
                  v-if="hasControl('correlationId')"
                  label="Request ID"
                  :error="isGuidOrEmpty(correlationDraft) ? undefined : 'Paste a full ID'"
                >
                  <UInput
                    v-model="correlationDraft"
                    placeholder="Every change from one request"
                    :ui="fieldUi"
                    class="w-full font-mono"
                    @update:model-value="commitText"
                  />
                </UFormField>

                <UFormField
                  v-if="hasControl('entityId')"
                  label="Item ID"
                  :error="isGuidOrEmpty(entityIdDraft) ? undefined : 'Paste a full ID'"
                >
                  <UInput
                    v-model="entityIdDraft"
                    placeholder="Account, job or incident ID"
                    :ui="fieldUi"
                    class="w-full font-mono"
                    @update:model-value="commitText"
                  />
                </UFormField>
              </template>
            </div>
          </div>
        </div>

        <div
          class="flex items-center justify-between gap-3 border-t border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70"
        >
          <p class="text-xs text-gray-600 tabular-nums dark:text-gray-400" aria-live="polite">
            {{ appliedSummary }}
          </p>

          <div class="flex items-center gap-2">
            <UButton
              label="Clear all"
              color="neutral"
              variant="ghost"
              size="sm"
              :disabled="activeCount === 0"
              @click="clearAll"
            />
            <UButton
              label="Done"
              color="neutral"
              variant="outline"
              size="sm"
              @click="isPanelOpen = false"
            />
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>
