<script setup lang="ts">
import { computed } from "vue";

import MediaArtwork from "./MediaArtwork.vue";

export interface MediaFanItem {
  key: string;
  seed: string;
  fileId?: string;
  versionId?: string;
  mimeType?: string;
}

const {
  items = [],
  fallbackSeed,
  size = "sm",
  wide = false,
} = defineProps<{
  items?: MediaFanItem[];
  fallbackSeed: string;
  size?: "sm" | "lg";
  wide?: boolean;
}>();

type FanShape = "sm-square" | "sm-wide" | "lg-square" | "lg-wide";

const FRAME: Record<FanShape, string> = {
  "sm-square": "h-11 w-[58px]",
  "sm-wide": "h-11 w-[58px]",
  "lg-square": "h-[76px] w-[104px]",
  "lg-wide": "h-[76px] w-[104px]",
};

const CARD: Record<FanShape, string> = {
  "sm-square": "top-[7px] size-[30px] rounded-[7px] border-2",
  "sm-wide": "top-[10px] h-6 w-[38px] rounded-[5px] border-2",
  "lg-square": "top-[11px] size-[54px] rounded-xl border-[3px]",
  "lg-wide": "top-[18px] h-10 w-[66px] rounded-[9px] border-[3px]",
};

const OFFSETS: Record<FanShape, readonly string[]> = {
  "sm-square": ["left-0.5", "left-3.5", "left-[26px]"],
  "sm-wide": ["left-0.5", "left-2.5", "left-[18px]"],
  "lg-square": ["left-1", "left-[25px]", "left-[46px]"],
  "lg-wide": ["left-1", "left-[19px]", "left-[34px]"],
};

const POSES = [
  "z-0 -rotate-12 group-hover:-translate-x-1 group-hover:-rotate-[20deg]",
  "z-10 -rotate-2 group-hover:-translate-y-1",
  "z-20 rotate-[9deg] group-hover:translate-x-1 group-hover:rotate-[17deg]",
] as const;

const shape = computed<FanShape>(() => `${size}-${wide ? "wide" : "square"}`);

const cards = computed(() =>
  [0, 1, 2].map((index) => {
    const item = items[index];

    return {
      key: item?.key ?? `${fallbackSeed}-${index}`,
      seed: item?.seed ?? `${fallbackSeed}-${index}`,
      fileId: item?.fileId,
      versionId: item?.versionId,
      mimeType: item?.mimeType,
      classes: [CARD[shape.value], OFFSETS[shape.value][index], POSES[index]],
    };
  }),
);
</script>

<template>
  <span class="relative block shrink-0" :class="FRAME[shape]" aria-hidden="true">
    <MediaArtwork
      v-for="card in cards"
      :key="card.key"
      :file-id="card.fileId"
      :version-id="card.versionId"
      :mime-type="card.mimeType"
      :seed="card.seed"
      floating
      class="border-white shadow-md transition-transform duration-200 ease-out motion-reduce:transition-none dark:border-neutral-800"
      :class="card.classes"
    />
  </span>
</template>
