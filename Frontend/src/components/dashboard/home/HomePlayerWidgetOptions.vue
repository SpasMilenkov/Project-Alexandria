<script setup lang="ts">
import type { HomePlayerOptions } from "@/types/home";

import HomeStylePicker from "@/components/dashboard/home/HomeStylePicker.vue";
import HomePlayerRecord from "@/components/dashboard/home/widgets/HomePlayerRecord.vue";
import { HomePlayerStyle } from "@/enums/home-widget";
import { HOME_PLAYER_STYLES, type HomeOptionsSection } from "@/utils/home-widget-editor";

const { options, section = "all" } = defineProps<{
  options: HomePlayerOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomePlayerOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Player style"
    description="Each player can have its own look."
  >
    <HomeStylePicker
      label="Player style"
      :styles="HOME_PLAYER_STYLES"
      :selected="options.playerStyle"
      @select="emit('change', { playerStyle: $event })"
    >
      <template #preview="{ value }">
        <div
          class="flex h-20 w-24 shrink-0 items-center justify-center gap-2 rounded-lg bg-gray-900 p-2 dark:bg-gray-900"
        >
          <HomePlayerRecord
            v-if="value === HomePlayerStyle.Vinyl"
            class="size-12 shrink-0"
            :src="null"
            :loaded="false"
            :spinning="false"
            icon="i-mdi-music-note-outline"
          />

          <div
            v-else
            class="flex size-12 shrink-0 items-center justify-center rounded-lg bg-gray-700 dark:bg-gray-700"
          >
            <UIcon
              name="i-mdi-music-note-outline"
              class="size-8 text-gray-200 dark:text-gray-200"
            />
          </div>

          <div class="flex w-6 flex-col items-center gap-2" aria-hidden="true">
            <span class="h-1 w-full rounded-full bg-gray-300 dark:bg-gray-300" />
            <span class="h-0.5 w-full rounded-full bg-gray-500 dark:bg-gray-500" />
            <UIcon name="i-mdi-play" class="size-6 text-gray-200 dark:text-gray-200" />
          </div>
        </div>
      </template>
    </HomeStylePicker>
  </UFormField>
</template>
