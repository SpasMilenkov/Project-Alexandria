<template>
  <UTooltip :text="tag.description ?? tag.name">
    <UBadge
      color="neutral"
      variant="outline"
      :label="tag.name"
      class="transition-colors duration-200 max-w-32"
      :icon="getIconByValue(tag.icon)"
      :style="{
        color: tag.color,
      }"
    >
      <template #trailing>
        <UButton
          icon="heroicons-x-mark"
          size="xs"
          variant="ghost"
          color="neutral"
          class="hover:cursor-pointer"
          v-if="fileId"
          @click="emit('removeTag', props.tag.id)"
        />
      </template>
    </UBadge>
  </UTooltip>
</template>
<script setup lang="ts">
import type { TagDto } from "@/api/tag";
import { getIconByValue } from "@/utils/icon.utils";

const props = defineProps<{
  tag: TagDto;
  fileId?: string;
}>();

const emit = defineEmits(["removeTag"]);
</script>
<style scoped></style>
