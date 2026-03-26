<template>
  <UModal :close="{ onClick: () => emit('close', false) }" title="Create New Tag">
    <template #body>
      <UForm :schema="createTagSchema" :state="state" class="space-y-5 w-full" @submit="onSubmit">
        <div class="grid grid-cols-2 gap-4">
          <UFormField label="Tag Name" name="name" required>
            <UInput
              v-model="state.name"
              class="w-full"
              placeholder="Enter tag name"
              icon="i-lucide-tag"
            />
          </UFormField>

          <UFormField label="Icon" name="icon" required>
            <USelectMenu
              v-model="state.icon"
              :items="iconOptions"
              value-key="value"
              searchable
              class="w-full"
              searchable-placeholder="Search icons..."
              placeholder="Select an icon"
            >
              <template #leading="{ modelValue }">
                <Icon
                  v-if="modelValue"
                  :icon="getIconByValue(modelValue as string) || 'mdi:tag'"
                  class="w-5 h-5 text-muted"
                />
              </template>
            </USelectMenu>
          </UFormField>
        </div>

        <UFormField label="Color" name="color" required>
          <div class="flex flex-wrap gap-2 p-1">
            <button
              v-for="color in settingsStore.AVAILABLE_COLORS"
              :key="color.name"
              type="button"
              @click="state.color = `rgb(${color.value})`"
              :class="[
                'w-7 h-7 sm:w-8 sm:h-8 rounded-full transition-all relative flex items-center justify-center',
                state.color === `rgb(${color.value})`
                  ? 'ring-2 ring-primary ring-offset-2 scale-105'
                  : 'hover:scale-110 ring-1 ring-gray-200/70 dark:ring-gray-700/70',
              ]"
              :style="{ backgroundColor: `rgb(${color.value})` }"
              :title="color.name.charAt(0).toUpperCase() + color.name.slice(1)"
            >
              <Icon
                v-if="state.color === `rgb(${color.value})`"
                icon="heroicons:check-16-solid"
                class="text-white w-4 h-4 drop-shadow-md"
              />
            </button>
          </div>
        </UFormField>

        <UFormField label="Preview">
          <div
            class="flex items-center justify-center p-6 border border-gray-200/70 dark:border-gray-700/70 rounded-lg bg-gray-50/40 dark:bg-white/5"
          >
            <div class="flex flex-col gap-4 w-full max-w-xs">
              <div>
                <p class="text-xs text-muted mb-2">Card</p>
                <TagCard
                  :is-selected="isSelected"
                  :tag="exampleTag"
                  :preview="true"
                  @click="isSelected = !isSelected"
                />
              </div>
              <div>
                <p class="text-xs text-muted mb-2">Badge</p>
                <TagBadge :tag="exampleTag" />
              </div>
            </div>
          </div>
        </UFormField>

        <UFormField label="Description" name="description">
          <UInput
            v-model="state.description"
            class="w-full"
            placeholder="Enter tag description (optional)"
          />
        </UFormField>

        <div class="flex gap-2 w-full justify-end pt-2">
          <UButton color="neutral" label="Cancel" variant="ghost" @click="emit('close', false)" />
          <UButton type="submit" :loading="isLoading" icon="i-lucide-plus">Create Tag</UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from "vue";
import { Icon } from "@iconify/vue";
import { useSettingsStore } from "@/stores/settings";
import { type CreateTagSchema, createTagSchema } from "@/schemas/tag";
import type { FormSubmitEvent } from "@nuxt/ui";
import { createTag } from "@/mutations/tags";
import TagCard from "../TagCard.vue";
import type { TagDto } from "@/api/tag";
import { getIconByValue, iconOptions } from "@/utils/icon.utils";
import TagBadge from "../TagBadge.vue";
import { logger } from "@/utils/logger";

const settingsStore = useSettingsStore();
const { mutateAsync, state: mutationState, isLoading } = createTag();
const emit = defineEmits<{ close: [boolean] }>();

const state = reactive<CreateTagSchema>({
  color: `rgb(${settingsStore.AVAILABLE_COLORS[0]?.value || ""})`,
  description: null,
  icon: "",
  name: "",
});

const onSubmit = async (event: FormSubmitEvent<CreateTagSchema>) => {
  await mutateAsync(event.data);
  if (!mutationState.value.error) {
    emit("close", true);
  }
};

const exampleTag = computed<TagDto>(() => ({
  color: state.color,
  createdAt: new Date().toISOString(),
  icon: state.icon || "tag",
  id: "preview-tag",
  name: state.name || "Example Tag",
  updatedAt: null,
  userId: "current-user",
}));

const isSelected = ref(false);
</script>

<style scoped>
.ring-offset-2 {
  --tw-ring-offset-color: var(--ui-bg);
}
</style>
