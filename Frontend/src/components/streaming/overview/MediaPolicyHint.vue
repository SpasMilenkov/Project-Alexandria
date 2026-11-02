<script setup lang="ts">
const FLOW = [
  { icon: "i-mdi-folder-outline", label: "Folder" },
  { icon: "i-mdi-tune-variant", label: "Transcode rule" },
  { icon: "i-mdi-play-circle-outline", label: "Ready to stream" },
] as const;
</script>

<template>
  <UPopover
    mode="hover"
    :open-delay="150"
    :close-delay="200"
    :content="{ side: 'top', align: 'start', sideOffset: 10 }"
    :ui="{
      content:
        'bg-default w-[min(22rem,calc(100vw-2rem))] rounded-2xl border border-gray-200/70 p-0 shadow-2xl ring-0 dark:border-gray-700/70',
    }"
  >
    <button
      type="button"
      aria-label="What is a transpilation policy?"
      class="inline-flex items-baseline gap-1 rounded font-semibold text-gray-900 underline decoration-gray-400 decoration-dotted underline-offset-4 hover:decoration-gray-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:text-gray-100 dark:decoration-gray-500 dark:hover:decoration-gray-100"
    >
      transpilation policy
      <UIcon
        name="i-mdi-information-outline"
        class="size-3.5 self-center text-gray-500 dark:text-gray-400"
        aria-hidden="true"
      />
    </button>

    <template #content>
      <div class="flex flex-col gap-4 p-4">
        <div class="flex items-start gap-3">
          <span
            class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-gray-500/15 text-gray-900 dark:text-gray-100"
          >
            <UIcon name="i-mdi-tune-variant" class="size-5" aria-hidden="true" />
          </span>

          <div class="min-w-0">
            <p class="text-lg font-semibold text-gray-900 dark:text-gray-100">
              What is a transpilation policy?
            </p>

            <p class="mt-1 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
              A policy is a set of automation rules attached to a folder. A Transcode rule converts
              every matching audio or video file uploaded there into streamable quality levels.
            </p>
          </div>
        </div>

        <ol
          class="flex items-stretch justify-between gap-1 rounded-xl bg-gray-500/10 p-2"
          aria-label="How a policy prepares media"
        >
          <template v-for="(step, index) in FLOW" :key="step.label">
            <li class="flex min-w-0 flex-1 flex-col items-center gap-1.5 px-1 py-1 text-center">
              <span
                class="flex size-8 items-center justify-center rounded-full bg-white text-gray-900 shadow-sm dark:bg-neutral-800 dark:text-gray-100"
              >
                <UIcon :name="step.icon" class="size-4" aria-hidden="true" />
              </span>

              <span class="text-xs font-medium text-gray-900 dark:text-gray-100">{{
                step.label
              }}</span>
            </li>

            <li v-if="index < FLOW.length - 1" class="flex items-center" aria-hidden="true">
              <UIcon name="i-mdi-chevron-right" class="size-4 text-gray-500 dark:text-gray-500" />
            </li>
          </template>
        </ol>

        <p class="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
          Set one up in Files: open a folder's details and add a Transcode rule under
          <span class="font-semibold text-gray-900 dark:text-gray-100">Automation</span>. Subfolders
          can inherit it.
        </p>
      </div>
    </template>
  </UPopover>
</template>
