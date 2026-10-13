<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { HomeClockOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { isAuthError } from "@/api/shuffle";
import { homeTimeZones } from "@/queries/home";
import {
  homeTimeZoneCatalogue,
  homeTimeZoneChoices,
  resolveHomeTimeZone,
} from "@/utils/home-time-zones";

import HomeClockAppearanceOptions from "./HomeClockAppearanceOptions.vue";

const { options, section = "all" } = defineProps<{
  options: HomeClockOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeClockOptions>] }>();

const {
  data: identifiers,
  isLoading,
  error,
  refresh,
} = useQuery(() => homeTimeZones({ enabled: section !== "style" }));

const catalogue = computed(() => homeTimeZoneCatalogue(identifiers.value ?? []));

const choices = computed(() => homeTimeZoneChoices(catalogue.value, options.timeZone));

const unavailable = computed(() => resolveHomeTimeZone(options.timeZone).unavailable);

const unlisted = computed(() => identifiers.value && !identifiers.value.includes(options.timeZone));

const authPending = computed(() => isAuthError(error.value));

const changeZone = (timeZone: string | undefined) => {
  if (timeZone) emit("change", { timeZone });
};
</script>

<template>
  <div class="flex min-w-0 flex-col gap-6">
    <HomeClockAppearanceOptions
      :options="options"
      :section="section"
      @change="emit('change', $event)"
    />

    <UFormField
      v-if="section !== 'style'"
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
        :model-value="options.timeZone"
        :items="choices"
        value-key="value"
        :filter-fields="['label', 'value']"
        :search-input="{ placeholder: 'Search time zones...' }"
        :content="{
          side: 'top',
          sticky: 'always',
          prioritizePosition: true,
          updatePositionStrategy: 'always',
        }"
        virtualize
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
  </div>
</template>
