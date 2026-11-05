<template>
  <UButton
    icon="i-mdi-content-copy"
    size="xs"
    color="neutral"
    variant="ghost"
    square
    class="shrink-0 justify-center transition-opacity duration-150 motion-reduce:transition-none md:opacity-0 md:group-hover:opacity-100 md:focus-visible:opacity-100"
    :aria-label="`Copy ${label.toLowerCase()}`"
    @click.stop="copyValue"
  />
</template>

<script setup lang="ts">
import { useClipboard } from "@vueuse/core";

import { useAppToast } from "@/composables/useAppToast";

const { value, label } = defineProps<{
  value: string;
  label: string;
}>();

const toast = useAppToast("clipboard");
const { copy } = useClipboard();

const copyValue = async () => {
  await copy(value);

  toast.success(`Copied ${label.toLowerCase()}`, { duration: 2500 });
};
</script>
