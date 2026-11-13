<script setup lang="ts">
const { name, email, avatarUrl = null } = defineProps<{
  name: string;
  email: string;
  avatarUrl?: string | null;
}>();

// Only places that really show the profile today.
const PLACES = [
  {
    description: "Your picture and name sit at the bottom of the sidebar on every page.",
    icon: "i-lucide-user-round",
    label: "Account menu",
  },
  {
    description: "Uploads, edits and deletions are recorded under your username.",
    icon: "i-lucide-history",
    label: "Activity history",
  },
  {
    description: "Files you own list you as their owner in the details panel.",
    icon: "i-lucide-folder-open",
    label: "File details",
  },
  {
    description: "Administrators see your name and picture in the user list.",
    icon: "i-lucide-users",
    label: "People",
  },
] as const;
</script>

<template>
  <div class="flex flex-col gap-8">
    <section aria-labelledby="profile-preview-title" class="flex flex-col gap-3">
      <p
        id="profile-preview-title"
        class="text-xs font-semibold tracking-widest text-gray-600 uppercase dark:text-gray-400"
      >
        How you'll appear
      </p>

      <div
        class="flex flex-col divide-y divide-gray-200/70 overflow-hidden rounded-xl border border-gray-200/70 bg-white dark:divide-gray-700/70 dark:border-gray-700/70 dark:bg-neutral-900"
        aria-hidden="true"
      >
        <div class="flex items-center gap-3 px-4 py-3">
          <UAvatar :src="avatarUrl ?? undefined" :alt="name" size="md" />

          <span class="flex min-w-0 flex-1 flex-col leading-tight">
            <span class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
              {{ name }}
            </span>
            <span class="truncate text-xs text-gray-500 dark:text-gray-500">{{ email }}</span>
          </span>

          <UIcon
            name="i-lucide-chevrons-up-down"
            class="size-4 shrink-0 text-gray-500 dark:text-gray-500"
          />
        </div>

        <div class="flex items-center gap-3 px-4 py-3">
          <UAvatar :src="avatarUrl ?? undefined" :alt="name" size="xs" />

          <p class="min-w-0 flex-1 truncate text-sm text-gray-700 dark:text-gray-300">
            <span class="font-medium text-gray-900 dark:text-gray-100">{{ name }}</span>
            uploaded <span class="font-medium text-gray-900 dark:text-gray-100">notes.pdf</span>
          </p>

          <span class="shrink-0 text-xs text-gray-500 dark:text-gray-500">just now</span>
        </div>
      </div>
    </section>

    <section aria-labelledby="profile-places-title" class="flex flex-col gap-3">
      <p
        id="profile-places-title"
        class="text-xs font-semibold tracking-widest text-gray-600 uppercase dark:text-gray-400"
      >
        Where it shows up
      </p>

      <ul class="flex flex-col gap-3">
        <li v-for="place in PLACES" :key="place.label" class="flex items-start gap-3">
          <span
            class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white text-gray-600 ring-1 ring-gray-200/70 dark:bg-neutral-900 dark:text-gray-400 dark:ring-gray-700/70"
          >
            <UIcon :name="place.icon" class="size-4" />
          </span>

          <span class="min-w-0">
            <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">
              {{ place.label }}
            </span>
            <span class="mt-0.5 block text-xs leading-relaxed text-gray-600 dark:text-gray-400">
              {{ place.description }}
            </span>
          </span>
        </li>
      </ul>
    </section>
  </div>
</template>
