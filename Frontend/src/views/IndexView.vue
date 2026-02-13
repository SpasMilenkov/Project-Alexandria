<template>
  <div
    class="flex flex-col grow  transition-colors items-center justify-center"
  >
    <div class="w-full px-6 py-12">
        <section
          class="text-center max-w-3xl mx-auto space-y-10"
        >
          <div class="flex justify-center">
            <div class="bg-primary p-5 rounded-2xl">
              <Icon icon="heroicons:book-open" class="w-16 h-16 text-white" />
            </div>
          </div>

          <div class="space-y-4">
            <h1
              class="text-5xl md:text-6xl font-bold text-gray-900 dark:text-white"
            >
              Welcome to Alexandria
            </h1>
            <p class="text-xl text-gray-600 dark:text-gray-400">
              Your personal digital library
            </p>
          </div>

          <div class="text-sm text-gray-500 dark:text-gray-500">
            Project initialized by
            <span class="text-primary dark:text-primary font-medium"
              >Spas Milenkov</span
            >
          </div>

          <div class="pt-6">
            <UButton
              @click="router.push('/dashboard')"
              size="xl"
              color="primary"
              variant="solid"
            >
              Enter your library
              <template #trailing>
                <Icon icon="heroicons:arrow-right" class="w-5 h-5" />
              </template>
            </UButton>
          </div>
        </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import router from "@/router";
</script>

<style scoped>
</style>
