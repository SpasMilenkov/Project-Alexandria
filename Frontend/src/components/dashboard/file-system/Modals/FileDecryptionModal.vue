<template>
  <UModal
    :open="true"
    :dismissible="!loading"
    @update:open="(v) => !v && !loading && emit('close')"
  >
    <template #content>
      <div
        class="bg-neutral-50 dark:bg-neutral-900 rounded-xl divide-y divide-neutral-200 dark:divide-neutral-800"
      >
        <!-- Header -->
        <div class="flex flex-col items-center gap-3 px-6 pt-7 pb-5 text-center">
          <div class="p-3 rounded-full bg-neutral-200 dark:bg-neutral-800">
            <Icon icon="mdi-lock" class="w-6 h-6 text-primary" />
          </div>
          <div>
            <h2 class="font-semibold text-base text-gray-900 dark:text-gray-100">Encrypted file</h2>
            <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
              Enter the password used when this file was uploaded.
            </p>
          </div>
        </div>

        <!-- Body -->
        <div class="px-6 py-5 flex flex-col gap-4">
          <!-- Error alert -->
          <UAlert
            v-if="error"
            color="error"
            variant="subtle"
            icon="i-mdi-alert-circle-outline"
            :title="error"
          />

          <!-- Password field -->
          <UFormField label="Password">
            <UInput
              ref="inputRef"
              v-model="password"
              :type="showPassword ? 'text' : 'password'"
              placeholder="Enter password…"
              autofocus
              :disabled="loading"
              class="w-full"
              @keydown.enter="handleSubmit"
            >
              <template #trailing>
                <UButton
                  :icon="showPassword ? 'i-mdi-eye-off-outline' : 'i-mdi-eye-outline'"
                  variant="ghost"
                  color="neutral"
                  size="xs"
                  :tabindex="-1"
                  aria-label="Toggle password visibility"
                  @click="showPassword = !showPassword"
                />
              </template>
            </UInput>
          </UFormField>

          <!-- Hint section — only rendered when hint exists -->
          <div v-if="props.hint" class="flex flex-col gap-1.5">
            <UButton
              :icon="showHint ? 'i-mdi-chevron-up' : 'i-mdi-lightbulb-outline'"
              :label="showHint ? 'Hide hint' : 'Show hint'"
              variant="ghost"
              color="neutral"
              size="xs"
              class="self-start -ml-1.5"
              @click="showHint = !showHint"
            />
            <Transition
              enter-active-class="transition-all duration-200 ease-out"
              enter-from-class="opacity-0 -translate-y-1"
              leave-active-class="transition-all duration-150 ease-in"
              leave-to-class="opacity-0 -translate-y-1"
            >
              <p
                v-if="showHint"
                class="text-sm text-gray-500 dark:text-gray-400 italic px-1 py-1.5 bg-neutral-100 dark:bg-neutral-800 rounded-md"
              >
                {{ props.hint }}
              </p>
            </Transition>
          </div>
        </div>

        <!-- Footer -->
        <div class="flex items-center justify-end gap-2 px-6 py-4">
          <UButton
            variant="outline"
            color="neutral"
            label="Cancel"
            :disabled="loading"
            @click="emit('close')"
          />
          <UButton
            variant="solid"
            color="primary"
            icon="i-mdi-download"
            label="Decrypt & Download"
            :loading="loading"
            :disabled="!password.trim() || loading"
            @click="handleSubmit"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { ref } from "vue";

const props = defineProps<{
  hint?: string | null;
  /**
   * Called by the parent composable with the entered password.
   * Should throw on decryption failure so the modal can display the error
   * and stay open for another attempt.
   * Should resolve on success — the modal then closes itself.
   */
  onAttempt: (password: string) => Promise<void>;
}>();

const emit = defineEmits<{
  close: [];
}>();

const password = ref("");
const showPassword = ref(false);
const showHint = ref(false);
const loading = ref(false);
const error = ref<string | null>(null);

const handleSubmit = async () => {
  if (!password.value.trim() || loading.value) return;

  loading.value = true;
  error.value = null;

  try {
    await props.onAttempt(password.value);
    // onAttempt resolved → download was triggered → close modal.
    emit("close");
  } catch (e: any) {
    error.value = e?.message ?? "Decryption failed";
    // Keep modal open so the user can try again.
  } finally {
    loading.value = false;
  }
};
</script>
