<script setup lang="ts">
import LoginForm from "@/components/LoginForm.vue";
</script>
<template>
  <div class="min-h-screen flex flex-1">
    <!-- Left Side - Illustration/Branding -->
    <div
      class="hidden lg:flex lg:w-1/2 bg-neutral items-center justify-center p-12 relative overflow-hidden border-r border-r-primary"
    >
      <!-- Decorative Elements - Centered circles -->
      <div class="absolute inset-0 flex items-center justify-center opacity-10">
        <div
          class="w-[500px] h-[500px] border-2 border-neutral rounded-full absolute"
        ></div>
        <div
          class="w-[600px] h-[600px] border-2 border-neutral rounded-full absolute"
        ></div>
      </div>

      <!-- Grid Pattern -->
      <div
        class="absolute inset-0 opacity-5 bg-[linear-gradient(var(--color-gray-900)_1px,transparent_1px),linear-gradient(90deg,var(--color-gray-900)_1px,transparent_1px)] bg-size-[50px_50px] dark:bg-[linear-gradient(white_1px,transparent_1px),linear-gradient(90deg,white_1px,transparent_1px)] dark:bg-size-[50px_50px]"
      ></div>

      <!-- Content - Centered -->
      <div
        class="max-w-md text-center relative z-10 flex flex-col items-center justify-center"
      >
        <h1 class="text-4xl font-bold mb-4">Welcome to Alexandria</h1>
        <p class="dark:text-primary-300 text-primary-500 text-lg">
          Your all in one solution for lazy file hosting
        </p>
        <!-- Feature Pills -->
        <div class="flex flex-wrap justify-center gap-3 mt-8">
          <div
            class="px-4 py-2 bg-primary/10 backdrop-blur-sm rounded-full border border-primary/20 text-sm"
          >
            Secure & Private
          </div>
          <div
            class="px-4 py-2 bg-primary/10 backdrop-blur-sm rounded-full border border-primary/20 text-sm"
          >
            Fast Access
          </div>  
        </div>
      </div>
    </div>

    <!-- Right Side - Login Form -->
    <div class="flex-1 flex items-center justify-center p-6">
      <div class="w-full max-w-md">
        <LoginForm />
      </div>
    </div>
  </div>
</template>
