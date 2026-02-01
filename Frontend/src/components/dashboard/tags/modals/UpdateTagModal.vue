<template>
  <UModal :close="{ onClick: () => emit('close', false) }" :title="'Edit Tag'">
    <template #body>
      <UForm
        :schema="updateTagSchema"
        :state="state"
        class="space-y-5 w-full"
        @submit="onSubmit"
      >
        <div class="grid grid-cols-2 gap-4">
          <UFormField label="Tag Name" name="name" required>
            <UInput
              v-model="state.name"
              class="w-full"
              placeholder="Enter tag name"
              icon="i-heroicons-tag"
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
              option-attribute="label"
              value-attribute="value"
              placeholder="Select an icon"
            >
              <template #leading="{ modelValue }">
                <Icon
                  v-if="modelValue"
                  :icon="getIconByValue(modelValue as string) || 'mdi:tag'"
                  class="w-5 h-5 text-gray-500"
                />
              </template>
            </USelectMenu>
          </UFormField>
        </div>

        <UFormField label="Color" name="color" required>
          <div class="grid grid-cols-8 sm:grid-cols-10 gap-2 p-1">
            <button
              v-for="color in settingsStore.AVAILABLE_COLORS"
              :key="color.name"
              type="button"
              @click="state.color = `rgb(${color.value})`"
              :class="[
                'w-6 h-6 sm:w-8 sm:h-8 rounded-full transition-all relative flex items-center justify-center',
                state.color === `rgb(${color.value})`
                  ? 'ring-2 ring-offset-2 ring-gray-900 dark:ring-white scale-110'
                  : 'hover:scale-110 ring-1 ring-gray-200 dark:ring-gray-700',
              ]"
              :style="{ backgroundColor: `rgb(${color.value})` }"
              :title="color.name.charAt(0).toUpperCase() + color.name.slice(1)"
            >
              <Icon
                v-if="state.color === `rgb(${color.value})`"
                icon="heroicons:check-16-solid"
                class="text-white w-4 h-4 sm:w-5 sm:h-5 drop-shadow-md"
              />
            </button>
          </div>
        </UFormField>

        <UFormField label="Preview">
          <div
            class="flex flex-wrap items-center justify-center gap-4 p-4 border border-primary/60 rounded-lg"
          >
            <div class="flex flex-col-reverse gap-3">
              <TagCard
                :is-selected="isPreviewSelected"
                @click="isPreviewSelected = !isPreviewSelected"
                :tag="exampleTag"
              />
              <TagBadge :tag="exampleTag"/>
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
          <UButton
            color="neutral"
            label="Cancel"
            variant="ghost"
            @click="emit('close', false)"
          />
          <UButton
            type="submit"
            :loading="mutationLoading"
            icon="i-heroicons-check"
          >
            Update Tag
          </UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { reactive, computed, ref } from "vue";
import { Icon } from "@iconify/vue";
import { useSettingsStore } from "@/stores/settings";
import { updateTagSchema, type UpdateTagSchema } from "@/schemas/tag";
import type { FormSubmitEvent } from "@nuxt/ui";
import { updateTag } from "@/mutations/tags";
import TagCard from "../TagCard.vue";
import { iconOptions, getIconByValue } from "@/utils/icon.utils";
import type { TagDto } from "@/api/tag";
import TagBadge from "../TagBadge.vue";

const props = defineProps<{
  tag: TagDto;
}>();

const emit = defineEmits<{ close: [boolean] }>();
const settingsStore = useSettingsStore();

const {
  mutateAsync,
  state: mutationState,
  isLoading: mutationLoading,
} = updateTag();

const state = reactive<UpdateTagSchema>({
  name: props.tag.name,
  icon: props.tag.icon,
  color: props.tag.color,
  description: props.tag.description,
});

const isPreviewSelected = ref(false);

const exampleTag = computed(() => ({
  id: props.tag.id,
  name: state.name,
  icon: getIconByValue(state.icon),
  color: state.color,
  description: state.description,
  userId: props.tag.userId,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
}));

const onSubmit = async (event: FormSubmitEvent<UpdateTagSchema>) => {
  await mutateAsync({
    tagId: props.tag.id,
    data: event.data,
  });
  if (!mutationState.value.error) {
    emit("close", true);
  }
};
</script>

<style scoped>
.ring-offset-2 {
  --tw-ring-offset-color: var(--ui-bg);
}
</style>
