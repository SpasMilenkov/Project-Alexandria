<script setup lang="ts">
import { computed } from "vue";

import type { UploadKind } from "@/composables/useUploadLauncher";

import { HomeQuickUploadStyle } from "@/enums/home-widget";

const {
  uploadStyle,
  over = false,
  preparing = false,
  disabled = false,
} = defineProps<{
  uploadStyle: HomeQuickUploadStyle;
  over?: boolean;
  preparing?: boolean;
  disabled?: boolean;
}>();

const emit = defineEmits<{ upload: [kind: UploadKind] }>();

const uploadMenu = computed(() => [
  {
    label: "Upload files",
    icon: "i-mdi-file-upload-outline",
    disabled,
    onSelect: () => emit("upload", "File"),
  },
  {
    label: "Upload folder",
    icon: "i-mdi-folder-upload-outline",
    disabled,
    onSelect: () => emit("upload", "Directory"),
  },
  {
    label: "Extract ZIP archive",
    icon: "i-mdi-archive-outline",
    disabled,
    onSelect: () => emit("upload", "Archive"),
  },
]);

const heading = computed(() => {
  if (preparing) return "Preparing your files…";
  if (over) return "Release to upload";

  return "Drop files or folders here";
});
</script>

<template>
  <div
    class="upload-body flex min-h-0 min-w-0 flex-1 gap-4 rounded-xl text-gray-900 dark:text-gray-100"
    :data-upload-style="uploadStyle"
    :data-drag-over="over"
    :class="{ 'upload-over': over }"
  >
    <div v-if="uploadStyle === HomeQuickUploadStyle.Pulse" class="upload-dial">
      <span class="pulse-ring pulse-first" aria-hidden="true" />
      <span class="pulse-ring pulse-second" aria-hidden="true" />

      <button
        type="button"
        class="pulse-target"
        aria-label="Upload files"
        :disabled="disabled"
        @click="emit('upload', 'File')"
      >
        <UIcon name="i-mdi-cloud-upload-outline" class="h-1/2 w-1/2" />
      </button>
    </div>

    <UIcon
      v-else-if="uploadStyle === HomeQuickUploadStyle.DropZone"
      :name="preparing ? 'i-mdi-loading' : 'i-mdi-cloud-upload-outline'"
      class="upload-icon size-8 shrink-0"
      :class="[
        over ? 'text-primary' : 'text-gray-600 dark:text-gray-400',
        { 'animate-spin': preparing },
      ]"
    />

    <div class="upload-details flex min-h-0 min-w-0 flex-1 flex-col justify-center gap-2">
      <div class="upload-copy min-w-0" :role="over || preparing ? 'status' : undefined">
        <p class="upload-heading text-sm font-semibold">{{ heading }}</p>
        <p class="upload-hint mt-1 text-xs text-gray-600 dark:text-gray-400">
          Files go to your library root.
        </p>
      </div>

      <div class="upload-controls flex min-w-0 items-center justify-center gap-2">
        <UButton
          v-if="uploadStyle !== HomeQuickUploadStyle.Pulse"
          color="primary"
          variant="solid"
          size="sm"
          :disabled="disabled"
          @click="emit('upload', 'File')"
          >Upload files</UButton
        >

        <UDropdownMenu :items="uploadMenu" :content="{ align: 'end' }">
          <UButton
            class="upload-overflow justify-center"
            color="neutral"
            variant="outline"
            size="sm"
            icon="i-mdi-dots-horizontal"
            aria-label="More upload options"
            :disabled="disabled"
          />
        </UDropdownMenu>
      </div>

      <div v-if="uploadStyle === HomeQuickUploadStyle.Actions" class="upload-secondary gap-2">
        <UButton
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="disabled"
          @click="emit('upload', 'Directory')"
          >Upload folder</UButton
        >
        <UButton
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="disabled"
          @click="emit('upload', 'Archive')"
          >Extract ZIP</UButton
        >
      </div>
    </div>
  </div>
</template>

<style scoped>
.upload-body {
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  text-align: center;
}

.upload-body[data-upload-style="0"] {
  border: 1px dashed var(--ui-border-accented);
}

.upload-over {
  outline: 2px solid var(--ui-primary);
  outline-offset: -2px;
  background: color-mix(in srgb, var(--ui-primary) 6%, transparent);
}

.upload-secondary {
  display: none;
  flex-wrap: wrap;
  justify-content: center;
}

.upload-dial {
  position: relative;
  display: grid;
  place-items: center;
  width: min(100cqi - 64px, 100cqb - 152px);
  min-width: 3rem;
  max-width: 18rem;
  aspect-ratio: 1;
  flex-shrink: 0;
}

.pulse-target {
  position: absolute;
  inset: 16%;
  display: grid;
  place-items: center;
  border: 1px solid var(--ui-border-accented);
  border-radius: 50%;
  color: var(--ui-text-muted);
  background: var(--ui-bg-elevated);
}

.pulse-target:hover,
.pulse-target:focus-visible {
  color: var(--ui-text);
  outline: 2px solid var(--ui-primary);
}

.pulse-ring {
  position: absolute;
  inset: 16%;
  border: 2px solid var(--ui-primary);
  border-radius: 50%;
  opacity: 0;
}

.upload-over .pulse-target {
  color: var(--ui-primary);
  border-color: var(--ui-primary);
}

.upload-over .pulse-ring {
  animation: upload-pulse 1.6s ease-out infinite;
}

.upload-over .pulse-second {
  animation-delay: 0.8s;
}

@keyframes upload-pulse {
  from {
    transform: scale(1);
    opacity: 0.7;
  }
  to {
    transform: scale(1.4);
    opacity: 0;
  }
}

@container home-widget (max-height: 200px) {
  .upload-body {
    padding: 0.5rem;
    gap: 0.5rem;
  }

  .upload-icon,
  .upload-hint {
    display: none;
  }

  .upload-dial {
    width: min(100cqi - 32px, 100cqb - 16px);
  }

  .upload-body[data-upload-style="2"] {
    position: relative;
    gap: 0;
  }

  .upload-body[data-upload-style="2"] .upload-copy {
    display: none;
  }

  .upload-body[data-upload-style="2"] .upload-details {
    position: absolute;
    right: 0;
    bottom: 0;
    flex: none;
  }
}

@container home-widget (min-height: 260px) {
  .upload-secondary {
    display: flex;
  }

  .upload-body[data-upload-style="1"] .upload-overflow {
    display: none;
  }
}

@container home-widget (min-width: 300px) and (min-aspect-ratio: 2 / 1) {
  .upload-body {
    flex-direction: row;
    text-align: left;
  }

  .upload-details {
    align-items: flex-start;
  }

  .upload-secondary {
    display: flex;
  }

  .upload-body[data-upload-style="1"] .upload-overflow {
    display: none;
  }

  .upload-dial {
    width: min(30cqi, 100cqb - 64px);
  }

  .upload-body[data-upload-style="2"] .upload-copy {
    display: block;
  }

  .upload-body[data-upload-style="2"] .upload-details {
    position: static;
    flex: 1;
  }
}

@container home-widget (max-width: 220px) {
  .upload-heading {
    font-size: 0.75rem;
  }

  .upload-controls {
    flex-wrap: wrap;
  }
}

@media (prefers-reduced-motion: reduce) {
  .upload-over .pulse-ring {
    animation: none;
  }
}
</style>
