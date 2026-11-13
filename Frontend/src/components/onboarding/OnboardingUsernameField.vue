<script setup lang="ts">
const draft = defineModel<string>({ required: true });

const { error = null, disabled = false } = defineProps<{
  error?: string | null;
  disabled?: boolean;
}>();

const emit = defineEmits<{ edit: [] }>();
</script>

<template>
  <UFormField
    label="Username"
    description="This is the name people see on your activity and files. You still sign in with your email."
    :error="error ?? undefined"
    :ui="{ description: 'text-gray-600 dark:text-gray-400' }"
  >
    <UInput
      v-model="draft"
      autocomplete="username"
      :maxlength="50"
      :disabled="disabled"
      size="lg"
      class="w-full"
      :ui="{ base: 'w-full' }"
      @update:model-value="emit('edit')"
    >
      <template #leading>
        <span class="text-sm text-gray-500 dark:text-gray-500">@</span>
      </template>
    </UInput>

    <template #help>
      <span class="text-xs text-gray-500 dark:text-gray-500">
        2 to 50 letters, numbers, dots, dashes or underscores.
      </span>
    </template>
  </UFormField>
</template>
