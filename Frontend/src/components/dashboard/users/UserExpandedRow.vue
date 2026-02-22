<template>
  <div class="px-5 py-5 bg-elevated/40 border-t border-default">
    <!-- Account overview cards -->
    <div class="grid grid-cols-2 xl:grid-cols-3 gap-3 mb-5">
      <!-- Internal ID -->
      <div
        class="rounded-lg border border-default bg-white/50 dark:bg-white/5 px-3.5 py-3 flex flex-col gap-1 min-w-0"
      >
        <div class="flex items-center gap-1.5">
          <UIcon
            name="i-lucide-fingerprint"
            class="size-3.5 text-muted shrink-0"
          />
          <span
            class="text-[10px] font-semibold uppercase tracking-wider text-muted"
          >
            Account ID
          </span>
          <UTooltip
            text="A unique internal identifier assigned to this account. Use it when referencing this user via the API."
            :delay-duration="200"
            class="ml-auto"
          >
            <UIcon
              name="i-lucide-info"
              class="size-3 text-muted/50 cursor-help shrink-0"
            />
          </UTooltip>
        </div>
        <p
          class="font-mono text-[11px] text-default break-all select-all leading-snug"
        >
          {{ user.id }}
        </p>
      </div>

      <!-- Last modified -->
      <div
        class="rounded-lg border border-default bg-white/50 dark:bg-white/5 px-3.5 py-3 flex flex-col gap-1"
      >
        <div class="flex items-center gap-1.5">
          <UIcon
            name="i-lucide-pencil-line"
            class="size-3.5 text-muted shrink-0"
          />
          <span
            class="text-[10px] font-semibold uppercase tracking-wider text-muted"
          >
            Last modified
          </span>
          <UTooltip
            text="The most recent time this account's details (name, email, role, etc.) were edited."
            :delay-duration="200"
            class="ml-auto"
          >
            <UIcon
              name="i-lucide-info"
              class="size-3 text-muted/50 cursor-help shrink-0"
            />
          </UTooltip>
        </div>
        <template v-if="user.updatedAt">
          <p class="text-sm font-medium text-default leading-tight">
            {{ formatDate(user.updatedAt) }}
          </p>
        </template>
        <template v-else>
          <p class="text-xs text-muted italic">Never modified</p>
        </template>
      </div>

      <!-- Access restriction -->
      <div
        class="rounded-lg border flex flex-col gap-1 px-3.5 py-3"
        :class="
          user.isLockedOut
            ? 'border-warning/40 bg-amber-50/60 dark:bg-amber-900/10'
            : 'border-default bg-white/50 dark:bg-white/5'
        "
      >
        <div class="flex items-center gap-1.5">
          <UIcon
            :name="user.isLockedOut ? 'i-lucide-lock' : 'i-lucide-lock-open'"
            class="size-3.5 shrink-0"
            :class="user.isLockedOut ? 'text-warning' : 'text-muted'"
          />
          <span
            class="text-[10px] font-semibold uppercase tracking-wider text-muted"
          >
            Access restriction
          </span>
          <UTooltip
            text="When a restriction is active the user cannot sign in. The lock is lifted automatically once the end date passes, or you can remove it manually."
            :delay-duration="200"
            class="ml-auto"
          >
            <UIcon
              name="i-lucide-info"
              class="size-3 text-muted/50 cursor-help shrink-0"
            />
          </UTooltip>
        </div>
        <template v-if="user.isLockedOut && user.lockedOutStarted">
          <p class="text-sm font-medium text-warning leading-tight">
            Restricted since {{ formatDate(user.lockedOutStarted) }}
          </p>
        </template>
        <template v-else-if="user.isLockedOut">
          <p class="text-sm font-medium text-warning leading-tight">
            Currently restricted
          </p>
        </template>
        <template v-else>
          <p class="text-xs text-muted italic">No active restriction</p>
        </template>
      </div>

      <!-- Account standing -->
      <div
        class="rounded-lg border flex flex-col gap-1 px-3.5 py-3"
        :class="
          user.deletedAt
            ? 'border-error/40 bg-red-50/60 dark:bg-red-900/10'
            : 'border-default bg-white/50 dark:bg-white/5'
        "
      >
        <div class="flex items-center gap-1.5">
          <UIcon
            :name="
              user.deletedAt ? 'i-lucide-trash-2' : 'i-lucide-shield-check'
            "
            class="size-3.5 shrink-0"
            :class="user.deletedAt ? 'text-error' : 'text-muted'"
          />
          <span
            class="text-[10px] font-semibold uppercase tracking-wider text-muted"
          >
            Account standing
          </span>
          <UTooltip
            text="Deleted accounts are soft-removed: their data is retained but they cannot sign in. You can permanently purge them or restore them later."
            :delay-duration="200"
            class="ml-auto"
          >
            <UIcon
              name="i-lucide-info"
              class="size-3 text-muted/50 cursor-help shrink-0"
            />
          </UTooltip>
        </div>
        <template v-if="user.deletedAt">
          <p class="text-sm font-medium text-error leading-tight">
            Deleted {{ formatDate(user.deletedAt) }}
          </p>
        </template>
        <template v-else>
          <p class="text-xs text-success italic font-medium">
            Account is active
          </p>
        </template>
      </div>

      <!-- File count -->
      <div
        class="rounded-lg border border-default bg-white/50 dark:bg-white/5 px-3.5 py-3 flex flex-col gap-1"
      >
        <div class="flex items-center gap-1.5">
          <UIcon name="i-lucide-files" class="size-3.5 text-muted shrink-0" />
          <span
            class="text-[10px] font-semibold uppercase tracking-wider text-muted"
          >
            Files
          </span>
          <UTooltip
            text="Total number of files owned by this account, including files in all folders."
            :delay-duration="200"
            class="ml-auto"
          >
            <UIcon
              name="i-lucide-info"
              class="size-3 text-muted/50 cursor-help shrink-0"
            />
          </UTooltip>
        </div>
        <template v-if="isFileCountLoading">
          <USkeleton class="h-5 w-16 rounded" />
        </template>
        <template v-else-if="fileCountError">
          <p class="text-xs text-error italic">Failed to load</p>
        </template>
        <template v-else>
          <p
            class="text-sm font-medium text-default leading-tight tabular-nums"
          >
            {{ fileCount?.toLocaleString() ?? "—" }}
            <span class="text-xs font-normal text-muted">
              {{ fileCount === 1 ? "file" : "files" }}
            </span>
          </p>
        </template>
      </div>

      <!-- Storage used -->
      <div
        class="rounded-lg border border-default bg-white/50 dark:bg-white/5 px-3.5 py-3 flex flex-col gap-1"
      >
        <div class="flex items-center gap-1.5">
          <UIcon
            name="i-lucide-hard-drive"
            class="size-3.5 text-muted flex-shrink-0"
          />
          <span
            class="text-[10px] font-semibold uppercase tracking-wider text-muted"
          >
            Storage used
          </span>
          <UTooltip
            text="Total disk space consumed by all files owned by this account."
            :delay-duration="200"
            class="ml-auto"
          >
            <UIcon
              name="i-lucide-info"
              class="size-3 text-muted/50 cursor-help flex-shrink-0"
            />
          </UTooltip>
        </div>
        <template v-if="isStorageLoading">
          <USkeleton class="h-5 w-20 rounded" />
        </template>
        <template v-else-if="storageError">
          <p class="text-xs text-error italic">Failed to load</p>
        </template>
        <template v-else>
          <p
            class="text-sm font-medium text-default leading-tight tabular-nums"
          >
            {{ storage !== undefined ? formatBytes(storage) : "—" }}
          </p>
        </template>
      </div>
    </div>

    <!-- Action strip -->
    <div
      class="flex flex-wrap items-center gap-2 pt-3.5 border-t border-default"
    >
      <UTooltip
        text="Edit this user's name, email, or role"
        :delay-duration="200"
      >
        <UButton
          size="xs"
          color="neutral"
          variant="outline"
          icon="i-lucide-pencil"
          label="Edit details"
          @click="$emit('edit', user)"
        />
      </UTooltip>

      <UTooltip
        :text="
          user.isLockedOut
            ? 'Lift the active restriction so this user can sign in again'
            : 'Temporarily block this user from signing in'
        "
        :delay-duration="200"
      >
        <UButton
          v-if="!user.isLockedOut"
          size="xs"
          color="warning"
          variant="outline"
          icon="i-lucide-lock"
          label="Restrict access"
          @click="$emit('restrict', user)"
        />
        <UButton
          v-else
          size="xs"
          color="success"
          variant="outline"
          icon="i-lucide-lock-open"
          label="Lift restriction"
          @click="$emit('restrict', user)"
        />
      </UTooltip>

      <UTooltip
        v-if="!user.deletedAt"
        text="Soft-delete this account. Data is preserved and can be restored."
        :delay-duration="200"
      >
        <UButton
          size="xs"
          color="error"
          variant="outline"
          icon="i-lucide-trash-2"
          label="Delete account"
          @click="$emit('delete', [user.id])"
        />
      </UTooltip>

      <span v-else class="text-xs text-muted italic ml-1">
        This account has been deleted — restore it by contacting a system
        administrator.
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import type { UserDetailsDto } from "@/types/user";
import { getUserCount, getUserStorage } from "@/queries/user";
import { formatDate } from "@/utils/date-formatters";
import { formatBytes } from "@/utils/size.utils";

const props = defineProps<{
  user: UserDetailsDto;
}>();

defineEmits<{
  edit: [user: UserDetailsDto];
  restrict: [user: UserDetailsDto];
  delete: [ids: string[]];
}>();

const {
  data: fileCount,
  isLoading: isFileCountLoading,
  error: fileCountError,
} = useQuery(getUserCount({ userId: props.user.id, deletedOnly: false }));

const {
  data: storage,
  isLoading: isStorageLoading,
  error: storageError,
} = useQuery(getUserStorage({ userId: props.user.id, deletedOnly: false }));
</script>
