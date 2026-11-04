<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import { type MoodSide, buildMoodPairs } from "@/features/audio-analysis/mood-pairs";
import { type MoodEntry, formatConfidence } from "@/features/audio-analysis/taxonomy";

import AudioConfidenceBar from "./AudioConfidenceBar.vue";

const { entries, color } = defineProps<{
  entries: MoodEntry[];
  color: string;
}>();

const BAR_STAGGER_MS = 80;

const layout = computed(() => buildMoodPairs(entries));

const leaningSummary = computed(() => {
  const names = layout.value.pairs.map((pair) =>
    (pair.leading === "left" ? pair.left.name : pair.right.name).toLowerCase(),
  );

  if (names.length < 2) return names.join("");

  return `${names.slice(0, -1).join(", ")} and ${names.at(-1)}`;
});

const nameClass = (emphasised: boolean) => {
  if (emphasised) return "font-semibold text-gray-900 dark:text-gray-100";

  return "text-gray-600 dark:text-gray-400";
};

// Any axis can land in the singles grid when its opposite pole is missing from the payload.
const MOOD_ICONS: Record<string, string> = {
  "mood_acoustic:acoustic": "lucide:guitar",
  "mood_party:party": "lucide:disc-3",
  "mood_happy:happy": "lucide:smile",
  "mood_sad:sad": "lucide:cloud-rain",
  "mood_relaxed:relaxed": "lucide:leaf",
  "mood_aggressive:aggressive": "lucide:flame",
};

const moodIcon = (key: string) => MOOD_ICONS[key] ?? "lucide:audio-lines";

// A faint accent wash behind the icon; the hex alpha suffix keeps it a tint in both themes.
const iconWash = computed(() => `${color}1f`);

const leads = (pair: { leading: MoodSide }, side: MoodSide) => pair.leading === side;
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="flex flex-col gap-1">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:emoticon-outline" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
        <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Mood balance</h3>
      </div>
      <p v-if="leaningSummary" class="text-xs text-gray-600 dark:text-gray-400">
        Leans {{ leaningSummary }}
      </p>
    </div>

    <div v-for="(pair, index) in layout.pairs" :key="pair.key" class="flex flex-col gap-2">
      <div class="flex items-baseline justify-between gap-3 text-sm">
        <span class="flex items-baseline gap-1.5">
          <span :class="nameClass(leads(pair, 'left'))">{{ pair.left.name }}</span>
          <span class="text-xs tabular-nums text-gray-600 dark:text-gray-400">
            {{ formatConfidence(pair.left.value) }}
          </span>
        </span>

        <span class="flex items-baseline gap-1.5">
          <span class="text-xs tabular-nums text-gray-600 dark:text-gray-400">
            {{ formatConfidence(pair.right.value) }}
          </span>
          <span :class="nameClass(leads(pair, 'right'))">{{ pair.right.name }}</span>
        </span>
      </div>

      <div class="relative grid grid-cols-2 items-center gap-2">
        <AudioConfidenceBar
          :value="pair.left.value"
          align="end"
          :color="leads(pair, 'left') ? color : undefined"
          :delay="index * BAR_STAGGER_MS"
        />
        <AudioConfidenceBar
          :value="pair.right.value"
          :color="leads(pair, 'right') ? color : undefined"
          :delay="index * BAR_STAGGER_MS"
        />

        <span
          class="absolute left-1/2 h-3 w-0.5 -translate-x-1/2 rounded-full bg-gray-400 dark:bg-gray-500"
          aria-hidden="true"
        />
      </div>
    </div>

    <dl v-if="layout.singles.length" class="m-0 grid grid-cols-1 gap-3 @sm:grid-cols-2">
      <div
        v-for="(mood, index) in layout.singles"
        :key="mood.key"
        class="flex min-w-0 overflow-hidden rounded-xl border border-gray-200/70 dark:border-gray-700/70"
      >
        <div
          class="flex w-16 shrink-0 items-center justify-center"
          :style="{ backgroundColor: iconWash }"
        >
          <Icon :icon="moodIcon(mood.key)" class="size-7" :style="{ color }" />
        </div>

        <div class="flex min-w-0 flex-1 flex-col justify-center gap-1 px-4 py-3">
          <dt class="truncate text-xs text-gray-600 dark:text-gray-400">{{ mood.name }}</dt>
          <dd class="m-0 text-2xl font-semibold tabular-nums text-gray-900 dark:text-gray-100">
            {{ formatConfidence(mood.value) }}
          </dd>
          <AudioConfidenceBar
            :value="mood.value"
            :color="color"
            :delay="(layout.pairs.length + index) * BAR_STAGGER_MS"
            size="sm"
          />
        </div>
      </div>
    </dl>
  </div>
</template>
