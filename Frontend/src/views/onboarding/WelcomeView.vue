<template>
  <div
    class="min-h-screen bg-gray-50 dark:bg-neutral-900 transition-colors flex items-center justify-center"
  >
    <div class="w-full px-6 py-12">
      <Transition
        mode="out-in"
        enter-active-class="transition duration-400 ease-out transform-gpu"
        enter-from-class="opacity-0 translate-y-4"
        enter-to-class="opacity-100 translate-y-0"
        leave-active-class="transition duration-300 ease-out transform-gpu"
        leave-from-class="opacity-100 translate-y-0"
        leave-to-class="opacity-0 -translate-y-4"
      >
        <section
          v-if="showGreeting"
          key="greeting"
          class="text-center max-w-3xl mx-auto space-y-10"
        >
          <div class="flex justify-center">
            <div class="bg-orange-500 p-5 rounded-2xl">
              <Icon icon="heroicons:book-open" class="w-16 h-16 text-white" />
            </div>
          </div>

          <div class="space-y-4">
            <h1 class="text-5xl md:text-6xl font-bold text-gray-900 dark:text-white">
              Welcome to Alexandria
            </h1>
            <p class="text-xl text-gray-600 dark:text-gray-400">Your personal digital library</p>
          </div>

          <div class="text-sm text-gray-500 dark:text-gray-500">
            Project initialized by
            <span class="text-orange-600 dark:text-orange-500 font-medium">Spas Milenkov</span>
          </div>

          <div class="pt-6">
            <UButton @click="showGreeting = false" size="xl" color="primary" variant="solid">
              Begin Your Initial Setup
              <template #trailing>
                <Icon icon="heroicons:arrow-right" class="w-5 h-5" />
              </template>
            </UButton>
          </div>
        </section>

        <section v-else key="configuration" class="max-w-7xl mx-auto space-y-10">
          <div class="space-y-6">
            <UButton @click="showGreeting = true" variant="ghost" color="gray" size="sm">
              <template #leading>
                <Icon icon="heroicons:arrow-left" class="w-4 h-4" />
              </template>
              Back
            </UButton>

            <div class="text-center space-y-3">
              <h2 class="text-4xl font-bold text-gray-900 dark:text-white">
                Available Configurations
              </h2>
              <p class="text-lg text-gray-600 dark:text-gray-400">
                Select the configuration that best fits your needs
              </p>
            </div>
          </div>

          <div class="grid md:grid-cols-2 gap-6 lg:gap-8">
            <UCard
              class="border border-gray-200 dark:border-gray-800 hover:border-orange-500 dark:hover:border-orange-500 transition-colors flex flex-col grow justify-between h-full"
              :ui="{
                body: 'flex flex-col grow justify-between gap-6',
              }"
            >
              <template #header>
                <div class="space-y-4">
                  <div class="flex items-center justify-between">
                    <div class="bg-orange-100 dark:bg-orange-950 p-3 rounded-xl">
                      <Icon
                        icon="heroicons:sparkles"
                        class="w-6 h-6 text-orange-600 dark:text-orange-500"
                      />
                    </div>
                    <UBadge color="orange" variant="subtle">Recommended</UBadge>
                  </div>
                  <h3 class="text-2xl font-bold text-gray-900 dark:text-white">
                    Standard Configuration
                  </h3>
                </div>
              </template>

              <p class="text-gray-700 dark:text-gray-300 leading-relaxed">
                The standard configuration offers a preconfigured out-of-the-box experience aimed at
                users with little to no technical knowledge. If you choose to proceed with this
                installation you will only have to select the features important to you based on the
                resources available on the machine where Alexandria would be running. Simple as
                that.
              </p>

              <div class="space-y-3">
                <div class="flex items-start gap-3">
                  <Icon
                    icon="heroicons:check-circle-solid"
                    class="w-5 h-5 text-orange-500 shrink-0 mt-0.5"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >Quick and easy setup process</span
                  >
                </div>
                <div class="flex items-start gap-3">
                  <Icon
                    icon="heroicons:check-circle-solid"
                    class="w-5 h-5 text-orange-500 shrink-0 mt-0.5"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >Pre-configured best practices</span
                  >
                </div>
                <div class="flex items-start gap-3">
                  <Icon
                    icon="heroicons:check-circle-solid"
                    class="w-5 h-5 text-orange-500 shrink-0 mt-0.5"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >Perfect for beginners</span
                  >
                </div>
              </div>

              <template #footer>
                <!-- TODO: Add proper routing once the setup is implemented -->
                <UButton
                  block
                  size="lg"
                  color="orange"
                  variant="solid"
                  @click="router.push('/setup')"
                >
                  Proceed to Standard Setup
                  <template #trailing>
                    <Icon icon="heroicons:arrow-right" class="w-5 h-5" />
                  </template>
                </UButton>
              </template>
            </UCard>

            <UCard
              class="border border-gray-200 dark:border-gray-800 hover:border-orange-500 dark:hover:border-orange-500 transition-colors flex flex-col justify-between"
              :ui="{
                body: 'flex flex-col grow justify-between gap-6',
              }"
            >
              <template #header>
                <div class="space-y-4">
                  <div class="flex items-center justify-between">
                    <div class="bg-gray-100 dark:bg-gray-900 p-3 rounded-xl">
                      <Icon
                        icon="heroicons:cog-6-tooth"
                        class="w-6 h-6 text-gray-600 dark:text-gray-400"
                      />
                    </div>
                    <UBadge color="gray" variant="subtle">For Power Users</UBadge>
                  </div>
                  <h3 class="text-2xl font-bold text-gray-900 dark:text-white">
                    Advanced Configuration
                  </h3>
                </div>
              </template>

              <p class="text-gray-700 dark:text-gray-300 leading-relaxed">
                The advanced configuration is aimed at more tech-savvy users who know what they are
                doing and want to customize their Alexandria deployments. For example some of you
                may not want to run Alexandria's default file management system and want to setup an
                alternative file hosting or streaming services like Plex or Nextcloud, or you may
                not want to default to MinIO and want to instead use Garage for your S3 storage. If
                your intents fall in this category this is the setup you should choose!
              </p>

              <div class="space-y-3">
                <div class="flex items-start gap-3">
                  <Icon
                    icon="heroicons:check-circle-solid"
                    class="w-5 h-5 text-orange-500 shrink-0 mt-0.5"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >Full customization control</span
                  >
                </div>
                <div class="flex items-start gap-3">
                  <Icon
                    icon="heroicons:check-circle-solid"
                    class="w-5 h-5 text-orange-500 shrink-0 mt-0.5"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >Alternative service integration</span
                  >
                </div>
                <div class="flex items-start gap-3">
                  <Icon
                    icon="heroicons:check-circle-solid"
                    class="w-5 h-5 text-orange-500 shrink-0 mt-0.5"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >Granular configuration options</span
                  >
                </div>
              </div>

              <template #footer>
                <!-- TODO: Add proper routing once the setup is implemented -->
                <UButton
                  block
                  size="lg"
                  color="orange"
                  variant="solid"
                  @click="router.push('/setup')"
                >
                  Proceed to Advanced Setup
                  <template #trailing>
                    <Icon icon="heroicons:arrow-right" class="w-5 h-5" />
                  </template>
                </UButton>
              </template>
            </UCard>
          </div>
        </section>
      </Transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { ref } from "vue";
import router from "@/router";
const showGreeting = ref(true);
</script>

<style scoped>
/* Custom styles if needed */
</style>
