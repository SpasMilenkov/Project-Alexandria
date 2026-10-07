<template>
  <main class="mx-auto max-w-[1000px] px-4 py-8 md:px-6" :style="paletteStyle">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <h1 class="text-3xl font-bold text-gray-900 dark:text-gray-100">Wrapped studio</h1>

      <div class="flex items-center gap-2">
        <WrappedPaletteSwitcher v-model="palette" />

        <WrappedExportButton
          :response="response"
          :year="new Date(response.from).getUTCFullYear()"
          :palette="palette"
        />
      </div>
    </div>

    <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
      Development fixtures. Compare equal totals with different listening habits.
    </p>

    <div class="my-6 flex flex-wrap gap-2">
      <button
        v-for="fixture in wrappedFixtures"
        :key="fixture.id"
        type="button"
        :aria-pressed="selected === fixture.id"
        class="rounded-lg border px-4 py-2 text-sm focus-visible:outline-2 focus-visible:outline-primary"
        :class="
          selected === fixture.id
            ? 'border-primary text-primary'
            : 'border-gray-300 text-gray-700 dark:border-gray-700 dark:text-gray-300'
        "
        @click="selected = fixture.id"
      >
        {{ fixture.name }}
      </button>
    </div>

    <label class="mb-4 flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
      <input v-model="alternateSeed" type="checkbox" /> Same facts, alternate decorative seed
    </label>

    <p class="mb-6 text-sm text-gray-600 dark:text-gray-400">{{ current.description }}</p>

    <WrappedExperience
      :response="response"
      :year="new Date(response.from).getUTCFullYear()"
      :palette="palette"
    />
  </main>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

import WrappedExperience from "@/components/streaming/wrapped/WrappedExperience.vue";
import WrappedExportButton from "@/components/streaming/wrapped/WrappedExportButton.vue";
import WrappedPaletteSwitcher from "@/components/streaming/wrapped/WrappedPaletteSwitcher.vue";
import { useWrappedPalette } from "@/composables/useWrappedPalette";
import { wrappedFixtures } from "@/utils/wrapped-fixtures";

const selected = ref("repeater");
const { palette, paletteStyle } = useWrappedPalette();
const alternateSeed = ref(false);

const current = computed(
  () => wrappedFixtures.find((fixture) => fixture.id === selected.value) ?? wrappedFixtures[0]!,
);

const response = computed(() => ({
  ...current.value.response,
  visualIdentity: current.value.response.visualIdentity + (alternateSeed.value ? "-alternate" : ""),
}));
</script>
