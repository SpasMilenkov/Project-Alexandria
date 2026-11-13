<script setup lang="ts">
import { computed, useTemplateRef } from "vue";

import AvatarCropModal from "@/components/dashboard/account/AvatarCropModal.vue";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import { useAvatarPicker } from "@/composables/useAvatarPicker";
import { useAvatarRemoval } from "@/composables/useAvatarRemoval";

const { name, avatarUrl = null } = defineProps<{
  name: string;
  avatarUrl?: string | null;
}>();

const fileInput = useTemplateRef<HTMLInputElement>("fileInput");
const { accept, clearPicked, handleFileChange, pickedFile } = useAvatarPicker();
const { confirming, handleConfirmClose, isRemoving } = useAvatarRemoval();

const openPicker = () => fileInput.value?.click();

const copy = computed(() => {
  if (avatarUrl) {
    return {
      action: "Change picture",
      description: "Looking good. You can swap it any time from My account.",
      title: "Your picture",
    };
  }

  return {
    action: "Upload a picture",
    description: "JPEG, PNG, WebP or an animated GIF. You'll see a preview before it's saved.",
    title: "Add a picture",
  };
});
</script>

<template>
  <section
    class="relative flex items-center gap-5 rounded-xl border border-gray-200/70 bg-neutral-50 p-4 sm:p-5 dark:border-gray-700/70 dark:bg-neutral-800"
    aria-labelledby="onboarding-avatar-title"
  >
    <UTooltip v-if="avatarUrl" text="Remove picture">
      <UButton
        icon="i-lucide-trash-2"
        color="error"
        variant="outline"
        size="xs"
        square
        class="absolute top-3 right-3 justify-center"
        aria-label="Remove profile picture"
        @click="confirming = true"
      />
    </UTooltip>

    <button
      type="button"
      class="group relative shrink-0 rounded-full focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary"
      :aria-label="copy.action"
      @click="openPicker"
    >
      <UAvatar
        v-if="avatarUrl"
        :src="avatarUrl"
        :alt="name"
        class="size-20 text-2xl shadow-md ring-4 ring-white dark:ring-neutral-900"
      />

      <span
        v-else
        class="flex size-20 items-center justify-center rounded-full border-2 border-dashed border-gray-300 bg-white text-gray-500 transition-colors duration-150 group-hover:border-gray-400 group-hover:text-gray-700 dark:border-gray-600 dark:bg-neutral-900 dark:text-gray-400 dark:group-hover:border-gray-500 dark:group-hover:text-gray-200"
      >
        <UIcon name="i-lucide-camera" class="size-7" />
      </span>
    </button>

    <div class="flex min-w-0 flex-col gap-2 pr-8">
      <div>
        <h2
          id="onboarding-avatar-title"
          class="text-sm font-semibold text-gray-900 dark:text-gray-100"
        >
          {{ copy.title }}
        </h2>
        <p class="mt-0.5 text-xs leading-relaxed text-gray-600 dark:text-gray-400">
          {{ copy.description }}
        </p>
      </div>

      <UButton
        :label="copy.action"
        icon="i-lucide-image-up"
        color="neutral"
        variant="outline"
        size="sm"
        class="self-start"
        @click="openPicker"
      />
    </div>

    <input
      ref="fileInput"
      type="file"
      :accept="accept"
      class="hidden"
      @change="handleFileChange"
    />

    <AvatarCropModal :file="pickedFile" @close="clearPicked" />

    <ConfirmModal
      :open="confirming"
      title="Remove your profile picture?"
      description="Your initials show in its place until you add a new one."
      confirm-label="Remove"
      confirm-icon="i-lucide-trash-2"
      danger-mode
      :loading="isRemoving"
      @close="handleConfirmClose"
    />
  </section>
</template>
