<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, nextTick, ref, useTemplateRef } from "vue";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useAvatarPicker } from "@/composables/useAvatarPicker";
import { useAvatarRemoval } from "@/composables/useAvatarRemoval";
import { useUsernameEditor } from "@/composables/useUsernameEditor";
import { getProfile } from "@/queries/user";
import { useAuthStore } from "@/stores/auth";
import { formatCalendarDate } from "@/utils/date-formatters";

import AvatarCropModal from "./AvatarCropModal.vue";
import ProfileStats from "./ProfileStats.vue";

const appToast = useAppToast("account");
const authStore = useAuthStore();

const { data: profile, isPending, error, refetch } = useQuery(getProfile());

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

// The session already knows name, email and picture, so the card never waits on the profile.
const displayName = computed(() => profile.value?.username ?? authStore.user?.user.name ?? "");
const email = computed(() => profile.value?.email ?? authStore.user?.user.email ?? "");
const avatarUrl = computed(
  () => profile.value?.avatarUrl ?? authStore.user?.user.avatarUrl ?? undefined,
);

const pictureHint = computed(() => {
  if (avatarUrl.value) return "Change profile picture";

  return "Add a profile picture";
});

// Without a picture the badge is the only hint the circle is clickable, so it stays visible.
const pictureBadgeClass = computed(() => {
  if (!avatarUrl.value) return undefined;

  return "pointer-fine:opacity-0 pointer-fine:group-hover:opacity-100 pointer-fine:group-focus-within:opacity-100";
});

const accountType = computed(() => {
  if (authStore.isAdmin) return "Administrator";

  return "Member";
});

// Picture

const fileInput = useTemplateRef<HTMLInputElement>("fileInput");
const { accept, clearPicked, handleFileChange, pickedFile } = useAvatarPicker();
const {
  confirming: confirmingRemove,
  handleConfirmClose: handleRemoveClose,
  isRemoving,
} = useAvatarRemoval();

const openPicker = () => fileInput.value?.click();

// Username

const editingName = ref(false);
const nameInput = useTemplateRef<{ inputRef?: HTMLInputElement }>("nameInput");
const {
  clearError: clearNameError,
  draft: draftName,
  error: nameError,
  isSaving: isSavingName,
  reset: resetName,
  save: saveDraftName,
} = useUsernameEditor(() => displayName.value);

const isNameDirty = computed(() => draftName.value.trim() !== displayName.value);

const startEditingName = async () => {
  resetName();
  editingName.value = true;

  await nextTick();
  nameInput.value?.inputRef?.select();
};

const cancelEditingName = () => {
  if (isSavingName.value) return;

  editingName.value = false;
  clearNameError();
};

const saveName = async () => {
  const result = await saveDraftName();
  if (result === "invalid") return;

  editingName.value = false;
  if (result === "saved") {
    appToast.success("Username updated", `You now appear as ${draftName.value.trim()}.`);
  }
};
</script>

<template>
  <section
    class="relative flex flex-col overflow-x-hidden overflow-y-auto rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    aria-label="Your profile"
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
        @click="confirmingRemove = true"
      />
    </UTooltip>

    <div class="flex flex-1 flex-col items-center justify-center gap-4 px-6 pt-8 pb-6 text-center">
      <UTooltip :text="pictureHint">
        <button
          type="button"
          class="group relative rounded-full focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary"
          :aria-label="pictureHint"
          @click="openPicker"
        >
          <UAvatar
            :src="avatarUrl"
            :alt="displayName"
            class="size-28 text-3xl shadow-xl ring-4 ring-white/70 dark:ring-neutral-800/70"
          />

          <span
            class="absolute right-0.5 bottom-0.5 flex size-9 items-center justify-center rounded-full bg-default text-gray-700 shadow-md ring-1 ring-gray-200/70 transition-opacity duration-150 ease-out motion-reduce:transition-none dark:text-gray-300 dark:ring-gray-700/70"
            :class="pictureBadgeClass"
            aria-hidden="true"
          >
            <UIcon name="i-lucide-pencil" class="h-4 w-4" />
          </span>
        </button>
      </UTooltip>

      <!-- The name itself is the edit target, swapped in place for a field the way track details are. -->
      <form
        v-if="editingName"
        class="flex w-full flex-col items-center gap-1"
        novalidate
        @submit.prevent="saveName"
        @keydown.esc.prevent="cancelEditingName"
      >
        <UFormField :error="nameError ?? undefined" class="w-full" :ui="{ error: 'text-center' }">
          <UInput
            ref="nameInput"
            v-model="draftName"
            aria-label="Username"
            autocomplete="username"
            variant="soft"
            :maxlength="50"
            class="w-full"
            :ui="{
              base: 'px-16 text-center text-xl font-semibold text-gray-900 dark:text-gray-100',
              trailing: 'pe-1',
            }"
            @update:model-value="clearNameError"
          >
            <template #trailing>
              <UTooltip text="Cancel">
                <UButton
                  icon="i-lucide-x"
                  color="neutral"
                  variant="link"
                  size="xs"
                  square
                  aria-label="Cancel"
                  :disabled="isSavingName"
                  @click="cancelEditingName"
                />
              </UTooltip>

              <UTooltip text="Save">
                <UButton
                  type="submit"
                  icon="i-lucide-check"
                  color="primary"
                  variant="link"
                  size="xs"
                  square
                  aria-label="Save username"
                  :loading="isSavingName"
                  :disabled="!isNameDirty"
                />
              </UTooltip>
            </template>
          </UInput>
        </UFormField>

        <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
          You still sign in with your email.
        </p>
      </form>

      <div v-else class="flex max-w-full min-w-0 flex-col items-center gap-1">
        <!-- A spacer the width of the pencil keeps the name centred under the picture. -->
        <div class="group flex max-w-full min-w-0 items-center gap-1.5">
          <span class="size-6 shrink-0" aria-hidden="true" />

          <h2 class="m-0 truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
            {{ displayName }}
          </h2>

          <UTooltip text="Change username">
            <UButton
              icon="i-lucide-pencil"
              color="neutral"
              variant="ghost"
              size="xs"
              square
              class="shrink-0 justify-center transition-opacity duration-150 motion-reduce:transition-none pointer-fine:opacity-0 pointer-fine:group-hover:opacity-100 pointer-fine:group-focus-within:opacity-100"
              aria-label="Change username"
              @click="startEditingName"
            />
          </UTooltip>
        </div>

        <div class="flex max-w-full min-w-0 items-center gap-2">
          <p class="m-0 truncate text-sm text-gray-600 dark:text-gray-400">{{ email }}</p>
          <UBadge
            v-if="authStore.isAdmin"
            label="Admin"
            color="neutral"
            variant="subtle"
            size="sm"
          />
        </div>
      </div>

      <p v-if="!avatarUrl" class="text-xs text-gray-600 dark:text-gray-400">
        Select the picture to add a JPEG, PNG, WebP or GIF. You can crop stills before saving.
      </p>
    </div>

    <div class="shrink-0 border-t border-gray-200/70 px-5 dark:border-gray-700/70">
      <ProfileStats />
    </div>

    <div class="shrink-0 border-t border-gray-200/70 px-5 dark:border-gray-700/70">
      <div v-if="visibleError" class="py-3">
        <UAlert
          color="error"
          variant="subtle"
          icon="i-lucide-alert-circle"
          title="Couldn't load your profile"
          :description="visibleError.message"
          :actions="[{ color: 'neutral', label: 'Try again', onClick: () => refetch() }]"
        />
      </div>

      <div v-else-if="isPending && !profile" class="flex h-24 items-center justify-center">
        <BlockSpinner :size="16" aria-label="Loading profile" class="text-gray-500" />
      </div>

      <dl v-else-if="profile" class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
        <div class="flex items-center justify-between gap-3 py-3">
          <dt class="text-xs font-medium text-gray-600 dark:text-gray-400">Member since</dt>
          <dd class="text-sm text-gray-900 dark:text-gray-100">
            <time :datetime="profile.createdAt">{{ formatCalendarDate(profile.createdAt) }}</time>
          </dd>
        </div>

        <div class="flex items-center justify-between gap-3 py-3">
          <dt class="text-xs font-medium text-gray-600 dark:text-gray-400">Account type</dt>
          <dd class="text-sm text-gray-900 dark:text-gray-100">{{ accountType }}</dd>
        </div>
      </dl>
    </div>

    <input ref="fileInput" type="file" :accept="accept" class="hidden" @change="handleFileChange" />

    <AvatarCropModal :file="pickedFile" @close="clearPicked" />

    <ConfirmModal
      :open="confirmingRemove"
      title="Remove your profile picture?"
      description="Your initials show in its place until you add a new one."
      confirm-label="Remove"
      confirm-icon="i-lucide-trash-2"
      danger-mode
      :loading="isRemoving"
      @close="handleRemoveClose"
    />
  </section>
</template>
