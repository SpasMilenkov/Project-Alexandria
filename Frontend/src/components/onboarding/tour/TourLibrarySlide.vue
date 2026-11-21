<template>
  <div class="flex flex-col gap-5">
    <div
      class="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/15 text-primary transition-colors duration-300"
    >
      <UIcon name="i-lucide-folder" class="h-6 w-6" />
    </div>

    <div>
      <h2 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
        Your library
      </h2>
      <p class="mt-2 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        These folders are created for you when you finish. Rename them now, or later from the
        explorer like any other folder. Notes keeps its name, since your notes live there.
      </p>
    </div>

    <ul class="flex flex-col gap-2.5">
      <li v-for="draft in drafts" :key="draft.kind" class="flex flex-col gap-1">
        <div class="flex items-center gap-3">
          <span
            class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-neutral-100 text-gray-600 dark:bg-neutral-800 dark:text-gray-400"
          >
            <UIcon :name="presetFor(draft.kind).icon" class="h-4 w-4" />
          </span>

          <UInput
            v-model="draft.name"
            size="sm"
            class="min-w-0 flex-1"
            :color="errors[draft.kind] ? 'error' : 'neutral'"
            :highlight="Boolean(errors[draft.kind])"
            :aria-label="`${presetFor(draft.kind).defaultName} folder name`"
            :aria-invalid="Boolean(errors[draft.kind])"
            :placeholder="presetFor(draft.kind).defaultName"
            :disabled="presetFor(draft.kind).fixedName"
            :trailing-icon="presetFor(draft.kind).fixedName ? 'i-lucide-lock' : undefined"
          />

          <UBadge
            v-if="isDraftAutomated(draft, capabilities)"
            icon="i-lucide-zap"
            label="Automated"
            color="neutral"
            variant="subtle"
            size="sm"
            class="shrink-0"
          />
        </div>

        <p v-if="errors[draft.kind]" class="ps-11 text-xs text-red-600 dark:text-red-400">
          {{ errors[draft.kind] }}
        </p>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import {
  type LibraryCapabilities,
  type LibraryErrors,
  type LibraryFolderDraft,
  isDraftAutomated,
  presetFor,
} from "@/features/onboarding/library-setup";

defineProps<{
  capabilities: LibraryCapabilities;
  errors: LibraryErrors;
}>();

const drafts = defineModel<LibraryFolderDraft[]>("drafts", { required: true });
</script>
