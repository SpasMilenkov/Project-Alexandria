<script setup lang="ts">
// Flex weights stand in for photo aspect ratios, so the ghost reads as justified rows of
// landscape and portrait shots rather than a uniform grid.
const MONTHS = [
  {
    key: "ghost-month-a",
    title: "w-32",
    rows: [
      [4, 3, 4, 2],
      [3, 4, 4, 3, 2],
    ],
  },
  {
    key: "ghost-month-b",
    title: "w-24",
    rows: [
      [2, 4, 3, 4],
      [4, 4, 3],
      [3, 2, 4, 4, 3],
    ],
  },
] as const;
</script>

<template>
  <div class="flex flex-col gap-8 p-4">
    <span class="h-7 w-24 rounded-xl bg-gray-500/25" />

    <div v-for="month in MONTHS" :key="month.key" class="flex flex-col gap-2">
      <div class="flex items-center gap-3 pb-1">
        <span class="h-4 rounded-xl bg-gray-500/25" :class="month.title" />
        <span class="h-3 w-16 rounded-xl bg-gray-500/15" />
      </div>

      <div
        v-for="(row, rowIndex) in month.rows"
        :key="rowIndex"
        class="flex h-32 gap-1.5 sm:h-40 sm:gap-3"
      >
        <span
          v-for="(weight, tileIndex) in row"
          :key="tileIndex"
          class="rounded-md bg-gray-500/15"
          :style="{ flexGrow: weight, flexBasis: 0 }"
        />
      </div>
    </div>
  </div>
</template>
