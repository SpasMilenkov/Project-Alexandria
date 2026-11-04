<template>
  <section
    class="overflow-hidden rounded-3xl border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
  >
    <div
      v-if="scene !== null"
      class="relative flex min-h-80 items-end overflow-hidden md:min-h-[340px]"
    >
      <WrappedSkyArt :scene="scene" />

      <div class="relative z-10 w-full px-6 pt-40 pb-6 md:px-8 md:pb-8">
        <p class="mb-2 text-xs tracking-[0.18em] text-[#e9eaf0] uppercase">
          {{ sceneLabel }} · your rhythm
        </p>

        <h2 class="max-w-[24ch] text-3xl leading-tight font-black text-white md:text-4xl">
          {{ card.headline }}
        </h2>

        <p class="mt-4 max-w-prose text-sm leading-relaxed text-[#f1f1f5]">{{ subline }}</p>
      </div>
    </div>
    <template v-else>
      <div class="grid grid-cols-5" aria-hidden="true">
        <div v-for="part in scenes" :key="part" class="relative h-40 md:h-56">
          <WrappedSkyArt :scene="part" compact />
        </div>
      </div>

      <div class="px-6 pt-6 md:px-8">
        <p class="text-xs tracking-[0.16em] text-gray-600 uppercase dark:text-gray-400">
          Your rhythm
        </p>

        <h2 class="mt-2 text-3xl font-black text-gray-900 dark:text-gray-100">
          {{ card.headline }}
        </h2>

        <p class="mt-4 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
          {{ subline }}
        </p>
      </div>
    </template>

    <div class="px-6 py-4 md:px-8">
      <WrappedStoryNotes :card="card" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { WrappedCardResponse } from "@/api/stats";

import WrappedSkyArt from "@/components/streaming/wrapped/WrappedSkyArt.vue";
import WrappedStoryNotes from "@/components/streaming/wrapped/WrappedStoryNotes.vue";
import { WrappedRhythmEvidence, WrappedTimeScene } from "@/enums/wrapped-story";
import { wrappedCardSubline } from "@/utils/wrapped-copy.utils";
import { skyPresets } from "@/utils/wrapped-sky.utils";

const props = defineProps<{ card: WrappedCardResponse }>();

const subline = computed(() => wrappedCardSubline(props.card));

const scenes = [
  WrappedTimeScene.Morning,
  WrappedTimeScene.Midday,
  WrappedTimeScene.Afternoon,
  WrappedTimeScene.Evening,
  WrappedTimeScene.Night,
];

const scene = computed(() => {
  const rhythm = props.card.facts?.rhythm;

  if (
    rhythm?.evidence !== WrappedRhythmEvidence.Pronounced ||
    rhythm.scene == null ||
    !skyPresets[rhythm.scene]
  )
    return null;

  return rhythm.scene;
});

const sceneLabel = computed(() =>
  scene.value === null ? "Throughout the day" : skyPresets[scene.value].eyebrow,
);
</script>
