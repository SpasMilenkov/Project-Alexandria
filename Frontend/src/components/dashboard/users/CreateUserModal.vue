<template>
  <UModal
    :open="open"
    :ui="{ width: 'sm:max-w-lg', header: 'p-0 sm:px-0' }"
    :close="false"
    :overlay="true"
  >
    <!-- ── Header ── -->
    <template #header>
      <div
        class="relative w-full overflow-hidden rounded-t-xl px-6 pt-6 pb-5 bg-linear-to-br from-primary/10 via-primary/5 to-transparent border-b border-default"
      >
        <!-- Decorative blobs -->
        <div
          class="absolute -top-8 -right-8 w-32 h-32 rounded-full bg-primary/8 pointer-events-none"
        />
        <div
          class="absolute -top-4 -right-4 w-16 h-16 rounded-full bg-primary/10 pointer-events-none"
        />

        <div class="flex items-center gap-3 relative">
          <div
            class="shrink-0 w-10 h-10 rounded-xl bg-primary/15 border border-primary/20 flex items-center justify-center shadow-sm"
          >
            <UIcon name="i-lucide-user-plus" class="size-5 text-primary" />
          </div>
          <div>
            <h2 class="text-base font-semibold text-default leading-tight">
              Create new account
            </h2>
            <p class="text-xs text-muted mt-0.5">
              The user will receive an invitation email.
            </p>
          </div>
        </div>

        <!-- Step indicators -->
        <div class="flex items-center gap-2 mt-4">
          <template v-for="(step, i) in STEPS" :key="i">
            <div
              class="flex items-center gap-1.5 text-xs transition-all duration-200"
              :class="
                currentStep === i
                  ? 'text-primary font-medium'
                  : currentStep > i
                    ? 'text-muted'
                    : 'text-muted/50'
              "
            >
              <div
                class="w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold border transition-all duration-200"
                :class="
                  currentStep > i
                    ? 'bg-primary/20 border-primary/30 text-primary'
                    : currentStep === i
                      ? 'bg-primary border-primary text-white shadow-sm shadow-primary/30'
                      : 'bg-transparent border-muted/40 text-muted/40'
                "
              >
                <UIcon
                  v-if="currentStep > i"
                  name="i-lucide-check"
                  class="size-2.5"
                />
                <span v-else>{{ i + 1 }}</span>
              </div>
              <span class="hidden sm:inline">{{ step.label }}</span>
            </div>
            <UIcon
              v-if="i < STEPS.length - 1"
              name="i-lucide-chevron-right"
              class="size-3 text-muted/40"
            />
          </template>
        </div>
      </div>
    </template>

    <!-- ── Body ── -->
    <template #body>
      <!--
        One UForm per step. This sidesteps all cross-step validation bleed
        and the "wrong button triggers submit" problem entirely — each form
        only knows about its own fields.
      -->

      <!-- Step 0 — Identity -->
      <UForm
        v-if="currentStep === 0"
        ref="formStep0"
        :schema="step0Schema"
        :state="state"
        class="space-y-4 py-2"
        @submit.prevent
      >
        <UFormField
          label="Username"
          name="userName"
          description="Letters, numbers, underscores, dots, hyphens. Max 50 chars."
        >
          <UInput
            v-model="state.userName"
            placeholder="e.g. jane.smith"
            icon="i-lucide-user"
            class="w-full"
            autofocus
          />
        </UFormField>
        <UFormField label="Email address" name="email">
          <UInput
            v-model="state.email"
            placeholder="user@example.com"
            icon="i-lucide-mail"
            type="email"
            class="w-full"
          />
        </UFormField>
      </UForm>

      <!-- Step 1 — Credentials -->
      <UForm
        v-else-if="currentStep === 1"
        ref="formStep1"
        :schema="step1Schema"
        :state="state"
        class="space-y-4 py-2"
        @submit.prevent
      >
        <UFormField
          label="Password"
          name="password"
          description="At least 8 characters."
        >
          <UInput
            v-model="state.password"
            :type="showPassword ? 'text' : 'password'"
            placeholder="••••••••"
            icon="i-lucide-lock"
            :trailing-icon="showPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
            class="w-full"
            @click-trailing="showPassword = !showPassword"
          />
        </UFormField>
        <UFormField label="Confirm password" name="confirmPassword">
          <UInput
            v-model="state.confirmPassword"
            :type="showConfirmPassword ? 'text' : 'password'"
            placeholder="••••••••"
            icon="i-lucide-lock-keyhole"
            :trailing-icon="
              showConfirmPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'
            "
            class="w-full"
            @click-trailing="showConfirmPassword = !showConfirmPassword"
          />
        </UFormField>

        <!-- Password strength meter -->
        <div v-if="state.password" class="space-y-1.5">
          <div class="flex gap-1">
            <div
              v-for="i in 4"
              :key="i"
              class="h-1 flex-1 rounded-full transition-all duration-300"
              :class="
                i <= passwordStrength.score
                  ? passwordStrength.color
                  : 'bg-gray-200 dark:bg-gray-700'
              "
            />
          </div>
          <p class="text-xs" :class="passwordStrength.textColor">
            {{ passwordStrength.label }}
          </p>
        </div>
      </UForm>

      <!-- Step 2 — Role & access -->
      <UForm
        v-else-if="currentStep === 2"
        ref="formStep2"
        :schema="step2Schema"
        :state="state"
        class="space-y-4 py-2"
        @submit="handleFinalSubmit"
      >
        <UFormField
          label="Role"
          name="role"
          description="Controls what the user can see and do."
        >
          <div class="grid grid-cols-2 gap-3 mt-1">
            <button
              v-for="option in ROLE_OPTIONS"
              :key="option.value"
              type="button"
              class="relative flex flex-col items-start gap-2 rounded-lg border-2 p-4 text-left transition-all duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
              :class="
                state.role === option.value
                  ? 'border-primary bg-primary/5 dark:bg-primary/10 shadow-sm'
                  : 'border-default bg-white/40 dark:bg-white/5 hover:border-primary/40'
              "
              @click="state.role = option.value"
            >
              <div
                class="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                :class="
                  state.role === option.value
                    ? 'bg-primary/15 text-primary'
                    : 'bg-elevated text-muted'
                "
              >
                <UIcon :name="option.icon" class="size-4" />
              </div>
              <div>
                <p class="text-sm font-medium text-default leading-tight">
                  {{ option.label }}
                </p>
                <p class="text-xs text-muted mt-0.5 leading-snug">
                  {{ option.description }}
                </p>
              </div>
              <div
                class="absolute top-3 right-3 w-4 h-4 rounded-full border-2 transition-all duration-150 flex items-center justify-center"
                :class="
                  state.role === option.value
                    ? 'border-primary bg-primary'
                    : 'border-muted/40'
                "
              >
                <UIcon
                  v-if="state.role === option.value"
                  name="i-lucide-check"
                  class="size-2.5 text-white"
                />
              </div>
            </button>
          </div>
        </UFormField>

        <!-- Summary -->
        <div
          v-if="state.userName && state.email"
          class="rounded-lg border border-default bg-elevated/40 p-4"
        >
          <p
            class="text-xs font-semibold uppercase tracking-wider text-muted mb-3 flex items-center gap-1.5"
          >
            <UIcon name="i-lucide-clipboard-check" class="size-3.5" />
            Summary
          </p>
          <div class="flex items-center gap-2.5">
            <div
              class="flex-shrink-0 w-7 h-7 rounded-full flex items-center justify-center text-xs font-semibold"
              :class="
                state.role === UserRole.Admin
                  ? 'bg-primary/15 text-primary'
                  : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300'
              "
            >
              {{ state.userName?.charAt(0)?.toUpperCase() ?? "?" }}
            </div>
            <div class="min-w-0">
              <p class="text-sm font-medium text-default truncate">
                @{{ state.userName }}
              </p>
              <p class="text-xs text-muted truncate">{{ state.email }}</p>
            </div>
            <UBadge
              :color="state.role === UserRole.Admin ? 'primary' : 'neutral'"
              variant="subtle"
              size="sm"
              class="ml-auto flex-shrink-0"
            >
              {{ state.role === UserRole.Admin ? "Admin" : "User" }}
            </UBadge>
          </div>
        </div>
      </UForm>
    </template>

    <!-- ── Footer ── -->
    <template #footer>
      <div class="flex items-center justify-between w-full gap-2">
        <UButton
          type="button"
          color="neutral"
          variant="ghost"
          label="Cancel"
          size="sm"
          @click="emit('close')"
        />

        <div class="flex items-center gap-2">
          <UButton
            v-if="currentStep > 0"
            type="button"
            color="neutral"
            variant="outline"
            icon="i-lucide-arrow-left"
            label="Back"
            size="sm"
            @click="currentStep--"
          />

          <!-- Next — validates current step's form before advancing -->
          <UButton
            v-if="currentStep < STEPS.length - 1"
            type="button"
            color="primary"
            variant="solid"
            trailing-icon="i-lucide-arrow-right"
            :label="STEPS[currentStep].next!"
            size="sm"
            @click="advanceStep"
          />

          <!-- Create — submits the final step's form -->
          <UButton
            v-else
            type="button"
            color="primary"
            variant="solid"
            icon="i-lucide-user-plus"
            label="Create account"
            size="sm"
            :loading="loading"
            @click="formStep2?.submit()"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { reactive, ref, computed, watch } from "vue";
import { z } from "zod";
import type { CreateUserSchema } from "@/schemas/user";
import { UserRole } from "@/enums/UserRole";

const props = defineProps<{
  open: boolean;
  loading?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [data: CreateUserSchema];
}>();

// Steps 

const STEPS: { label: string; next?: string }[] = [
  { label: "Identity", next: "Set password" },
  { label: "Credentials", next: "Choose role" },
  { label: "Role & access" },
];

const ROLE_OPTIONS = [
  {
    label: "User",
    value: UserRole.User,
    icon: "i-lucide-user",
    description: "Standard access — browse and manage own content.",
  },
  {
    label: "Administrator",
    value: UserRole.Admin,
    icon: "i-lucide-shield-check",
    description: "Full access — manage users, settings, and all content.",
  },
];

// Per-step schemas

const step0Schema = z.object({
  userName: z
    .string()
    .min(2, "Must be at least 2 characters")
    .max(50, "Max 50 characters")
    .regex(
      /^[a-zA-Z0-9_.\-]+$/,
      "Only letters, numbers, underscores, dots, and hyphens",
    ),
  email: z.email("Invalid email address"),
});

const step1Schema = z
  .object({
    password: z.string().min(8, "Must be at least 8 characters"),
    confirmPassword: z.string(),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: "Passwords don't match",
    path: ["confirmPassword"],
  });

const step2Schema = z.object({
  role: z.enum(UserRole).default(UserRole.User),
});

//  Local state 

const formStep0 = ref();
const formStep1 = ref();
const formStep2 = ref();

const currentStep = ref(0);
const showPassword = ref(false);
const showConfirmPassword = ref(false);

const state = reactive<CreateUserSchema>({
  userName: "",
  email: "",
  password: "",
  confirmPassword: "",
  role: UserRole.User,
});

// Reset every time the modal opens.
watch(
  () => props.open,
  (isOpen) => {
    if (!isOpen) return;
    Object.assign(state, {
      userName: "",
      email: "",
      password: "",
      confirmPassword: "",
      role: UserRole.User,
    });
    currentStep.value = 0;
    showPassword.value = false;
    showConfirmPassword.value = false;
  },
);

// Password strength

const passwordStrength = computed(() => {
  const p = state.password;
  let score = 0;
  if (p.length >= 8) score++;
  if (p.length >= 12) score++;
  if (/[A-Z]/.test(p) && /[0-9]/.test(p)) score++;
  if (/[^A-Za-z0-9]/.test(p)) score++;

  const levels = [
    { label: "Too weak", color: "bg-red-400", textColor: "text-red-500" },
    { label: "Weak", color: "bg-orange-400", textColor: "text-orange-500" },
    {
      label: "Fair",
      color: "bg-yellow-400",
      textColor: "text-yellow-600 dark:text-yellow-400",
    },
    {
      label: "Strong",
      color: "bg-emerald-400",
      textColor: "text-emerald-600 dark:text-emerald-400",
    },
    {
      label: "Very strong",
      color: "bg-emerald-500",
      textColor: "text-emerald-600 dark:text-emerald-400",
    },
  ];

  return { score: Math.max(score, 1), ...levels[Math.max(score - 1, 0)] };
});

// Step navigation 

const currentForm = computed(() => {
  if (currentStep.value === 0) return formStep0.value;
  if (currentStep.value === 1) return formStep1.value;
  return null; // step 2 submits directly
});

async function advanceStep() {
  try {
    await currentForm.value?.validate();
    currentStep.value++;
  } catch {
    // Inline errors shown by UForm — nothing else needed.
  }
}

//  Final submit
function handleFinalSubmit(event: { data: Pick<CreateUserSchema, "role"> }) {
  // Merge the validated role with the rest of the already-validated state.
  emit("submit", { ...state, role: event.data.role });
}
</script>
