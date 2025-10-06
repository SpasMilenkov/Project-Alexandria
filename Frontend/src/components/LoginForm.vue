<script setup lang="ts">
import type { FormSubmitEvent } from '@nuxt/ui'
import { reactive } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { loginSchema, type LoginSchema } from '@/schemas/auth'
import { useRouter } from 'vue-router'
const authStore = useAuthStore()
const router = useRouter()
const state = reactive<Partial<LoginSchema>>({
  email: undefined,
  password: undefined,
})

const toast = useToast()
async function onSubmit(event: FormSubmitEvent<LoginSchema>) {
  toast.add({ title: 'Success', description: 'The form has been submitted.', color: 'success' })
  console.log(event.data)
  const result = await authStore.login(event.data)
  if(result.success) router.push("/dashboard")
}
</script>

<template>
  <UCard>
    <template #header> Welcome </template>

    <UForm :schema="loginSchema" :state="state" class="space-y-4" @submit="onSubmit">
      <UFormField label="Email" name="email">
        <UInput v-model="state.email" />
      </UFormField>

      <UFormField label="Password" name="password">
        <UInput v-model="state.password" type="password" />
      </UFormField>

      <UButton type="submit"> Submit </UButton>
    </UForm>
  </UCard>
</template>
