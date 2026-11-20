<template>
  <ExplorerTooltipCard icon="i-lucide-folder" :name="data.name" subtitle="Folder">
    <ExplorerTooltipStat label="Size" hint="Including subfolders" emphasis>
      {{ sizeLabel }}
    </ExplorerTooltipStat>

    <ExplorerTooltipStat label="Modified">{{ modifiedLabel }}</ExplorerTooltipStat>

    <ExplorerTooltipStat label="Created">{{ createdLabel }}</ExplorerTooltipStat>

    <ExplorerTooltipStat label="Owner">{{ data.ownerUserDto.name }}</ExplorerTooltipStat>

    <template v-if="data.isStarred" #notes>
      <ExplorerTooltipNote icon="i-lucide-star">In your favorites</ExplorerTooltipNote>
    </template>
  </ExplorerTooltipCard>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import { formatTimeAgo } from "@/utils/date-formatters";
import { formatBytes } from "@/utils/size";

import ExplorerTooltipCard from "./ExplorerTooltipCard.vue";
import ExplorerTooltipNote from "./ExplorerTooltipNote.vue";
import ExplorerTooltipStat from "./ExplorerTooltipStat.vue";

const { data } = defineProps<{
  data: DirectorySummaryDto;
}>();

// The size already covers subfolders, so the folder needs no extra fetch to fill the card.
const sizeLabel = computed(() => formatBytes(data.size));

const modifiedLabel = computed(() => formatTimeAgo(data.updatedAt || data.createdAt));

const createdLabel = computed(() => formatTimeAgo(data.createdAt));
</script>
