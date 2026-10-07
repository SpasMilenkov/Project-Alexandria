<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { HomeWidget, HomeWidgetOptions } from "@/types/home";

import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { HomeShortcutGroup } from "@/enums/home-widget";
import { homeTimeZones } from "@/queries/home";
import { homeTimeZoneChoices, resolveHomeTimeZone } from "@/utils/home-time-zones";

const { widget } = defineProps<{ widget: HomeWidget }>();
const emit = defineEmits<{ change: [options: HomeWidgetOptions] }>();

const {
  data: identifiers,
  isLoading,
  error,
  refresh,
} = useQuery(() =>
  homeTimeZones({
    enabled: HOME_WIDGET_REGISTRY[widget.type].optionControl === "time-zone",
  }),
);

const choices = computed(() =>
  homeTimeZoneChoices(identifiers.value ?? [], widget.options.timeZone),
);

const unavailable = computed(() => resolveHomeTimeZone(widget.options.timeZone).unavailable);

const unlisted = computed(
  () => identifiers.value && !identifiers.value.includes(widget.options.timeZone),
);

const isAuthError = (err: unknown): boolean => {
  if (!err) return false;

  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;

  if (status === 401 || status === 403) return true;

  const message = String(e.message ?? "").toLowerCase();

  return (
    message.includes("unauthorized") || message.includes("401") || message.includes("forbidden")
  );
};

const authPending = computed(() => isAuthError(error.value));

const shortcutGroups = [
  { label: "Files and folders", value: HomeShortcutGroup.Files },
  { label: "Music and videos", value: HomeShortcutGroup.Media },
];

const changeZone = (timeZone: string | undefined) => {
  if (timeZone) emit("change", { ...widget.options, timeZone });
};

const changeGroup = (shortcutGroup: HomeShortcutGroup) =>
  emit("change", { ...widget.options, shortcutGroup });
</script>

<template>
  <div class="min-w-0">
    <UFormField
      v-if="HOME_WIDGET_REGISTRY[widget.type].optionControl === 'time-zone'"
      label="Time zone"
      description="Choose the place this clock follows."
    >
      <div v-if="(isLoading && !identifiers) || authPending" class="flex justify-center p-4">
        <UIcon
          name="i-mdi-loading"
          class="size-5 animate-spin text-primary"
          aria-label="Loading time zones"
        />
      </div>

      <div v-else-if="!identifiers" class="flex flex-col items-start gap-2" role="alert">
        <p class="text-sm text-gray-600 dark:text-gray-400">Time zones could not be loaded.</p>

        <UButton color="neutral" variant="outline" @click="refresh()">Retry time zones</UButton>
      </div>

      <USelectMenu
        v-else
        :model-value="widget.options.timeZone"
        :items="choices"
        value-key="value"
        :filter-fields="['label', 'value']"
        :search-input="{ placeholder: 'Search time zones...' }"
        aria-label="Time zone"
        class="w-full"
        @update:model-value="changeZone"
      />

      <p v-if="unavailable" class="mt-2 text-sm text-gray-600 dark:text-gray-400" role="status">
        This device cannot display your saved time zone. The clock is showing device time.
      </p>

      <p v-else-if="unlisted" class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Your saved time zone is not in the current list. It is kept until you choose another.
      </p>
    </UFormField>

    <UFormField v-else label="Shortcut group" description="Choose which destinations appear here.">
      <USelect
        :model-value="widget.options.shortcutGroup"
        :items="shortcutGroups"
        aria-label="Shortcut group"
        class="w-full"
        @update:model-value="changeGroup"
      />
    </UFormField>
  </div>
</template>
