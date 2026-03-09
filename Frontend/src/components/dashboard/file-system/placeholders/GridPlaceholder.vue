<template>
  <!-- Folders section skeleton -->
  <div class="mb-6">
    <div class="flex items-center gap-2 ml-2 pb-4">
      <div class="skeleton-pill w-16 h-4 rounded" />
    </div>
    <div
      class="grid gap-3 grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8"
    >
      <div
        v-for="i in folderCount"
        :key="`folder-${i}`"
        class="skeleton-card flex flex-col items-center gap-2 p-3 rounded-xl border border-primary/5"
        :style="{ '--delay': `${i * 40}ms` }"
      >
        <!-- Folder icon -->
        <div class="skeleton-block w-10 h-8 rounded-lg mt-1" />
        <!-- Name -->
        <div class="skeleton-block rounded w-3/4 h-3" />
        <!-- Sub-line -->
        <div class="skeleton-block rounded w-1/2 h-2.5" />
      </div>
    </div>
  </div>

  <!-- Files section skeleton -->
  <div>
    <div class="skeleton-pill w-10 h-4 rounded mb-4 ml-1" />
    <div
      class="grid gap-3 grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8"
    >
      <div
        v-for="i in fileCount"
        :key="`file-${i}`"
        class="skeleton-card flex flex-col items-center gap-2 p-3 rounded-xl border border-primary/5"
        :style="{ '--delay': `${(i + folderCount) * 40}ms` }"
      >
        <!-- Thumbnail / icon area -->
        <div class="skeleton-block w-full aspect-4/3 rounded-lg" />
        <!-- Name -->
        <div class="skeleton-block rounded w-4/5 h-3" />
        <!-- Size / meta -->
        <div class="skeleton-block rounded w-2/5 h-2.5" />
      </div>
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
    folderCount: 4,
    fileCount: 8,
  },
);
</script>

<style scoped>
/* ─── Shimmer keyframe ─────────────────────────────────────────── */
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
    transform: translateY(4px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* ─── Shared shimmer surface ───────────────────────────────────── */
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

/* ─── Card wrapper ─────────────────────────────────────────────── */
.skeleton-card {
  animation:
    fade-in 0.3s ease both,
    shimmer 1.6s ease-in-out infinite;
  animation-delay: var(--delay, 0ms), var(--delay, 0ms);
  background: color-mix(in srgb, var(--ui-bg-muted) 40%, transparent);
}

/* Override shimmer on card — only the inner blocks shimmer */
.skeleton-card {
  animation: fade-in 0.3s ease both;
  animation-delay: var(--delay, 0ms);
}

/* Section label pill */
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
