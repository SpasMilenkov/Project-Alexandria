<template>
  <section
    class="min-w-0 overflow-hidden rounded-3xl border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
  >
    <header class="px-6 pt-6">
      <p class="text-xs tracking-[0.16em] uppercase" :style="{ color: inks[0] }">
        {{ isTrack ? "The mixtape" : "Your record collection" }}
      </p>

      <h2 class="mt-2 text-2xl font-black tracking-tight text-gray-900 dark:text-gray-100">
        {{ isTrack ? "Your tracks. Your signature." : "The names behind your year." }}
      </h2>

      <p class="mt-3 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        {{ introduction }}
      </p>
    </header>

    <ol class="px-6 pt-4 pb-6">
      <li
        v-for="record in records"
        :key="record.entry.entityId ?? record.entry.rank"
        class="relative border-b border-gray-200/70 py-5 last:border-0 dark:border-gray-700/70"
      >
        <div class="flex items-start gap-4">
          <div
            v-if="!isTrack"
            class="relative flex h-20 w-20 shrink-0 items-center justify-center self-center"
            aria-hidden="true"
          >
            <svg viewBox="0 0 96 96" class="h-full w-full">
              <circle
                cx="48"
                cy="48"
                :r="record.radius"
                :fill="inks[(record.entry.rank - 1) % 3]"
              />

              <circle
                v-for="groove in [0.6, 0.73, 0.86]"
                :key="groove"
                cx="48"
                cy="48"
                :r="record.radius * groove"
                fill="none"
                stroke="var(--wrapped-cutout)"
                stroke-width="0.8"
              />

              <circle cx="48" cy="48" :r="record.radius * 0.33" fill="var(--wrapped-cutout)" />
              <circle cx="48" cy="48" :r="record.radius * 0.055" fill="var(--wrapped-pin)" />
            </svg>
          </div>
          <span
            v-else
            class="w-8 shrink-0 pt-1 font-mono text-2xl font-light"
            :style="{ color: inks[(record.entry.rank - 1) % 3] }"
            >{{ padRank(record.entry.rank) }}</span
          >

          <div class="min-w-0 flex-1">
            <p
              v-if="!isTrack"
              class="mb-1 text-xs font-medium tracking-wide text-gray-600 dark:text-gray-400"
            >
              {{ padRank(record.entry.rank)
              }}<span v-if="record.entry.rank === 1"> · YOUR HEADLINER</span>
            </p>

            <RouterLink
              v-if="isTrack && record.entry.entityId"
              :to="{ name: 'track-details', params: { fileId: record.entry.entityId } }"
              class="block rounded text-lg leading-snug font-bold break-words text-gray-900 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100"
              >{{ record.entry.title }}</RouterLink
            >
            <p
              v-else
              class="text-lg leading-snug font-bold break-words text-gray-900 dark:text-gray-100"
              :class="{ 'text-2xl': record.entry.rank === 1 }"
            >
              {{ record.entry.title }}
            </p>

            <p
              v-if="isTrack && record.entry.artist"
              class="mt-1 text-sm break-words text-gray-600 dark:text-gray-400"
            >
              {{ record.entry.artist }}
            </p>

            <p class="mt-2 text-xs leading-relaxed text-gray-600 dark:text-gray-400">
              {{ record.entry.subtitle }}
            </p>

            <div class="mt-3 flex items-center gap-3">
              <div
                v-if="isTrack"
                class="h-2 min-w-0 flex-1 overflow-hidden rounded-sm bg-gray-200/70 dark:bg-gray-700/70"
                aria-hidden="true"
              >
                <div
                  class="h-full rounded-sm"
                  :style="{
                    width: `${record.ratio * 100}%`,
                    backgroundColor: inks[(record.entry.rank - 1) % 3],
                  }"
                />
              </div>

              <span
                class="text-xs font-semibold tabular-nums"
                :style="{ color: inks[(record.entry.rank - 1) % 3] }"
                >{{ shareLabel(record.entry.share)
                }}<span v-if="!isTrack" class="font-normal text-gray-600 dark:text-gray-400">
                  of your listening</span
                ></span
              >
            </div>
          </div>
        </div>
      </li>
    </ol>

    <p
      class="border-t border-gray-200/70 px-6 py-4 text-xs leading-relaxed text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
    >
      {{
        isTrack
          ? "Longer bars mean more time with that song."
          : "Bigger records mean more time with that artist."
      }}
      Percentages use all your listening.
    </p>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { RouterLink } from "vue-router";

import type { WrappedCardResponse } from "@/api/stats";

import { WrappedCardType } from "@/enums/wrapped-card-type";
import { wrappedInks } from "@/utils/wrapped-art.utils";
import { padRank } from "@/utils/wrapped-display.utils";
import { rankedRecords, shareLabel } from "@/utils/wrapped-ranking.utils";

const props = defineProps<{ card: WrappedCardResponse }>();

const isTrack = computed(() => props.card.type === WrappedCardType.TopSongs);

const records = computed(() => rankedRecords(props.card.entries));

const inks = computed(() => wrappedInks(props.card));

const introduction = computed(() => {
  const leader = records.value[0]?.entry;

  if (!leader) return "Your favorites are still taking shape.";
  if (isTrack.value) return "The songs that earned their place, one listen at a time.";

  return `${leader.title} led the way with ${shareLabel(leader.share)} of your listening.`;
});
</script>
