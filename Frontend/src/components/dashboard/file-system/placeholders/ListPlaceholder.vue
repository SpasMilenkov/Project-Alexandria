<template>
  <!-- Folders section -->
  <div class="pt-4">
    <div class="skeleton-pill w-16 h-3.5 rounded mx-4 mb-3" />

    <div
      v-for="i in folderCount"
      :key="`folder-row-${i}`"
      class="skeleton-row flex items-center gap-3 px-4 py-2.5"
      :style="{ '--delay': `${i * 35}ms` }"
    >
      <!-- Icon -->
      <div class="skeleton-block w-8 h-8 rounded-lg shrink-0" />
      <!-- Name + meta stacked -->
      <div class="flex flex-col gap-1.5 flex-1 min-w-0">
        <div class="skeleton-block rounded h-3" :style="{ width: rowWidth(i, 'folder') }" />
        <div class="skeleton-block rounded h-2.5 w-24" />
      </div>
      <!-- Action dots -->
      <div class="skeleton-block w-6 h-6 rounded-full shrink-0 opacity-50" />
    </div>
  </div>

  <!-- Files section -->
  <div class="mt-4">
    <div class="skeleton-pill w-10 h-3.5 rounded mx-4 mb-3" />

    <div
      v-for="i in fileCount"
      :key="`file-row-${i}`"
      class="skeleton-row flex items-center gap-3 px-4 py-2.5"
      :style="{ '--delay': `${(i + folderCount) * 35}ms` }"
    >
      <!-- Thumbnail -->
      <div class="skeleton-block w-8 h-8 rounded-lg shrink-0" />
      <!-- Name -->
      <div class="flex flex-col gap-1.5 flex-1 min-w-0">
        <div class="skeleton-block rounded h-3" :style="{ width: rowWidth(i, 'file') }" />
        <div class="skeleton-block rounded h-2.5 w-20" />
      </div>
      <!-- Size pill -->
      <div class="skeleton-block w-12 h-5 rounded-full shrink-0 opacity-60" />
      <!-- Action dots -->
      <div class="skeleton-block w-6 h-6 rounded-full shrink-0 opacity-50" />
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    folderCount?: number;
    fileCount?: number;
  }>(),
  {
    folderCount: 3,
    fileCount: 6,
  },
);

// Vary row widths so they feel organic, not robotic
const widths = ["72%", "58%", "83%", "65%", "77%", "50%", "88%", "62%"];
function rowWidth(i: number, _type: string) {
  return widths[(i - 1) % widths.length];
}
</script>

<style scoped>
@keyframes shimmer {
  0% {
    background-position: -200% 0;
  }
  100% {
    background-position: 200% 0;
  }
}

@keyframes fade-in {
  from {
    opacity: 0;
    transform: translateX(-3px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

.skeleton-block {
  background: linear-gradient(
    90deg,
    color-mix(in srgb, var(--ui-color-neutral-200) 60%, transparent) 25%,
    color-mix(in srgb, var(--ui-color-neutral-100) 80%, transparent) 50%,
    color-mix(in srgb, var(--ui-color-neutral-200) 60%, transparent) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.6s ease-in-out infinite;
}

.dark .skeleton-block {
  background: linear-gradient(
    90deg,
    color-mix(in srgb, var(--ui-color-neutral-700) 60%, transparent) 25%,
    color-mix(in srgb, var(--ui-color-neutral-600) 50%, transparent) 50%,
    color-mix(in srgb, var(--ui-color-neutral-700) 60%, transparent) 75%
  );
  background-size: 200% 100%;
}

.skeleton-row {
  animation: fade-in 0.25s ease both;
  animation-delay: var(--delay, 0ms);
  border-radius: 0.5rem;
}

.skeleton-pill {
  background: linear-gradient(
    90deg,
    color-mix(in srgb, var(--ui-color-neutral-200) 60%, transparent) 25%,
    color-mix(in srgb, var(--ui-color-neutral-100) 80%, transparent) 50%,
    color-mix(in srgb, var(--ui-color-neutral-200) 60%, transparent) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.6s ease-in-out infinite;
}

.dark .skeleton-pill {
  background: linear-gradient(
    90deg,
    color-mix(in srgb, var(--ui-color-neutral-700) 60%, transparent) 25%,
    color-mix(in srgb, var(--ui-color-neutral-600) 50%, transparent) 50%,
    color-mix(in srgb, var(--ui-color-neutral-700) 60%, transparent) 75%
  );
  background-size: 200% 100%;
}
</style>
