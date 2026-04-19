<script setup lang="ts">
import LoginForm from "@/components/LoginForm.vue";
</script>

<template>
  <div class="min-h-screen flex flex-1">
    <!-- Left Side — desktop only -->
    <div
      class="hidden lg:flex lg:w-1/2 bg-neutral items-center justify-center p-12 relative overflow-hidden border-r border-r-primary"
    >
      <div
        class="absolute inset-0 opacity-5 bg-[linear-gradient(var(--color-gray-900)_1px,transparent_1px),linear-gradient(90deg,var(--color-gray-900)_1px,transparent_1px)] bg-size-[48px_48px] dark:bg-[linear-gradient(white_1px,transparent_1px),linear-gradient(90deg,white_1px,transparent_1px)]"
      ></div>
      <!-- Concentric rings -->
      <div class="absolute inset-0 flex items-center justify-center">
        <div class="w-[360px] h-[360px] border border-primary/15 rounded-full absolute"></div>
        <div class="w-[520px] h-[520px] border border-primary/15 rounded-full absolute"></div>
        <div class="w-[680px] h-[680px] border border-primary/15 rounded-full absolute"></div>
      </div>
      <!-- Dot grid accent -->
      <div class="absolute bottom-12 right-12 grid grid-cols-5 gap-1.5 opacity-20">
        <span v-for="n in 25" :key="n" class="w-1 h-1 bg-primary rounded-full block" />
      </div>
      <!-- Content -->
      <div class="relative z-10 max-w-sm text-center flex flex-col items-center">
        <h1 class="text-4xl font-bold mb-3 leading-tight tracking-tight">
          Welcome to<br /><em class="text-primary not-italic">Alexandria</em>
        </h1>
        <p class="text-gray-500 dark:text-gray-400 text-base font-light">
          Your all-in-one solution for lazy file hosting
        </p>
        <div class="flex flex-wrap justify-center gap-2 mt-8">
          <span
            class="px-3.5 py-1.5 bg-primary/20 dark:bg-primary/10 border border-primary/35 dark:border-primary/20 rounded-full text-xs font-semibold text-gray-700 dark:text-gray-200"
            >Secure & Private</span
          >
          <span
            class="px-3.5 py-1.5 bg-primary/20 dark:bg-primary/10 border border-primary/35 dark:border-primary/20 rounded-full text-xs font-semibold text-gray-700 dark:text-gray-200"
            >Fast Access</span
          >
          <span
            class="px-3.5 py-1.5 bg-primary/20 dark:bg-primary/10 border border-primary/35 dark:border-primary/20 rounded-full text-xs font-semibold text-gray-700 dark:text-gray-200"
            >Organized</span
          >
        </div>
      </div>
    </div>

    <!-- Right Side -->
    <div class="flex-1 flex flex-col items-center justify-center p-6">
      <!-- Mobile header — hidden on desktop -->
      <div
        class="lg:hidden flex flex-col items-center text-center mb-9 animate-[slideDown_0.4s_ease_both]"
      >
        <span class="text-2xl font-bold tracking-tight">Alexandria</span>
        <p class="text-sm text-gray-500 dark:text-gray-400 font-light mb-4">
          Your all-in-one lazy file host
        </p>
        <!-- Decorative accent divider -->
        <div class="flex items-center gap-2.5 w-full max-w-xs">
          <div class="flex-1 h-px bg-gray-200 dark:bg-gray-800"></div>
          <div class="w-1.5 h-1.5 rounded-full bg-primary"></div>
          <div class="flex-1 h-px bg-gray-200 dark:bg-gray-800"></div>
        </div>
      </div>

      <!-- Form -->
      <div class="w-full max-w-md animate-[fadeUp_0.45s_ease_0.1s_both]">
        <LoginForm />
      </div>
    </div>
  </div>
</template>

<style>
@keyframes float {
  0%,
  100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-6px);
  }
}
@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-14px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
@keyframes fadeUp {
  from {
    opacity: 0;
    transform: translateY(18px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
