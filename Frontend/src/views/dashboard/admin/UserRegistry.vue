<template>
  <div class="flex h-full min-h-0 flex-col gap-3 p-3">
    <header
      aria-labelledby="user-registry-heading"
      class="shrink-0 rounded-xl border border-gray-200/70 px-4 py-4 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex min-w-0 items-center gap-2.5">
          <UIcon name="i-lucide-users" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />

          <div class="min-w-0">
            <h1
              id="user-registry-heading"
              class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100"
            >
              User Registry
            </h1>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              Manage accounts, roles, and access restrictions.
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <UButton
            v-if="selectedUserIds.length > 0"
            color="error"
            variant="outline"
            icon="i-lucide-trash-2"
            :label="`Delete ${selectedUserIds.length} selected`"
            size="sm"
            @click="openDelete(selectedUserIds)"
          />
          <UButton
            color="primary"
            variant="solid"
            icon="i-lucide-user-plus"
            label="New user"
            size="sm"
            @click="openCreate()"
          />
        </div>
      </div>

      <div
        class="mt-4 flex flex-col gap-4 max-md:rounded-xl max-md:border max-md:border-gray-200/70 max-md:bg-black/5 max-md:p-4 max-md:dark:border-gray-700/70 max-md:dark:bg-white/5"
      >
        <div class="flex flex-col gap-4 md:flex-row md:items-center">
          <UInput
            v-model="uiState.userName"
            icon="i-lucide-search"
            placeholder="Search by username"
            aria-label="Search by username"
            :maxlength="50"
            :ui="fieldUi"
            class="w-full md:max-w-xl md:flex-1"
            @update:model-value="debouncedCommit"
          />

          <div class="flex w-full items-center gap-2 md:ml-auto md:w-auto">
            <USelect
              v-model="sortBy"
              :items="sortByOptions"
              aria-label="Sort users"
              :ui="fieldUi"
              class="w-full md:w-44"
            />
            <UButton
              variant="outline"
              color="neutral"
              :title="sortDirectionTitle"
              :aria-label="sortDirectionLabel"
              :icon="sortDirectionIcon"
              class="h-10 w-10 shrink-0 justify-center rounded-xl"
              @click="toggleSortDirection"
            />
            <UButton
              icon="i-lucide-sliders-horizontal"
              label="Filters"
              :color="filterButtonColor"
              :variant="filterButtonVariant"
              :aria-expanded="isFilterPanelOpen"
              aria-controls="user-filter-panel"
              class="h-10 shrink-0 rounded-xl px-3"
              @click="isFilterPanelOpen = !isFilterPanelOpen"
            >
              <template v-if="advancedFilterCount > 0" #trailing>
                <span
                  class="flex size-5 items-center justify-center rounded-full bg-primary text-xs font-semibold text-inverted tabular-nums"
                >
                  {{ advancedFilterCount }}
                </span>
              </template>
            </UButton>
          </div>
        </div>

        <div class="flex flex-col gap-4 md:flex-row md:flex-wrap md:items-center">
          <div class="flex flex-col gap-2 md:flex-row md:items-center">
            <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 md:pr-1">
              Accounts
            </span>
            <div
              role="group"
              aria-label="Accounts"
              class="flex gap-1 rounded-xl bg-black/5 p-1 dark:bg-white/5 md:inline-flex"
            >
              <UButton
                v-for="option in accountOptions"
                :key="option.value"
                :label="option.label"
                :aria-pressed="accountsMode === option.value"
                :color="segmentColor(accountsMode === option.value)"
                :variant="segmentVariant(accountsMode === option.value)"
                class="h-8 flex-1 justify-center rounded-lg px-4 md:flex-none"
                @click="accountsMode = option.value"
              />
            </div>
          </div>

          <span
            aria-hidden="true"
            class="hidden h-8 w-px bg-gray-200/70 dark:bg-gray-700/70 md:block"
          />

          <div class="flex flex-col gap-2 md:flex-row md:items-center">
            <span class="text-sm font-semibold text-gray-600 dark:text-gray-400 md:pr-1">
              Lock
            </span>
            <div
              role="group"
              aria-label="Lock status"
              class="flex gap-1 rounded-xl bg-black/5 p-1 dark:bg-white/5 md:inline-flex"
            >
              <UButton
                v-for="option in lockOptions"
                :key="option.value"
                :label="option.label"
                :aria-pressed="lockMode === option.value"
                :color="segmentColor(lockMode === option.value)"
                :variant="segmentVariant(lockMode === option.value)"
                class="h-8 flex-1 justify-center rounded-lg px-4 md:flex-none"
                @click="lockMode = option.value"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Animating the row track instead of toggling the node keeps the table
           below moving in step with the panel rather than snapping. -->
      <div
        id="user-filter-panel"
        class="grid transition-[grid-template-rows,opacity]"
        :class="filterPanelClass"
        :inert="!isFilterPanelOpen"
      >
        <div class="min-h-0 overflow-hidden">
          <div
            class="mt-4 max-h-[40vh] space-y-4 overflow-y-auto border-t border-gray-200/70 pt-4 dark:border-gray-700/70"
          >
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <UFormField label="Email" :error="emailError">
                <UInput
                  v-model="emailDraft"
                  type="email"
                  icon="i-lucide-mail"
                  placeholder="jane@example.com"
                  :maxlength="256"
                  :ui="fieldUi"
                  class="w-full"
                  @update:model-value="debouncedEmailCommit"
                />
              </UFormField>

              <UFormField label="Role">
                <USelect v-model="roleFilter" :items="roleOptions" :ui="fieldUi" class="w-full" />
              </UFormField>
            </div>

            <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div v-for="filter in DATE_FILTERS" :key="filter.key" class="min-w-0 space-y-1">
                <DateRangeFilter
                  :model-value="getRange(filter)"
                  :label="filter.label"
                  :icon="filter.icon"
                  :disabled="isDateFilterDisabled(filter)"
                  @update:model-value="setRange(filter, $event)"
                />
                <p
                  v-if="isDateFilterDisabled(filter)"
                  class="px-1 text-xs text-gray-500 dark:text-gray-500"
                >
                  Include deleted accounts to use this
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </header>

    <section
      aria-label="Users"
      class="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    >
      <div
        class="flex shrink-0 flex-wrap items-center gap-2 border-b border-gray-200/70 px-4 py-3 dark:border-gray-700/70 sm:px-6"
      >
        <p
          class="mr-2 text-sm font-semibold text-gray-900 tabular-nums dark:text-gray-100"
          role="status"
          aria-live="polite"
        >
          {{ resultSummary }}
        </p>

        <span
          v-for="pill in activeFilterPills"
          :key="pill.key"
          class="inline-flex items-center gap-1 rounded-lg bg-primary/10 py-1 pr-1 pl-3 text-sm font-semibold text-primary"
        >
          {{ pill.label }}
          <UButton
            icon="i-lucide-x"
            size="xs"
            color="primary"
            variant="ghost"
            :aria-label="`Remove ${pill.label} filter`"
            class="justify-center rounded-md"
            @click="pill.clear()"
          />
        </span>

        <UButton
          v-if="activeFilterPills.length > 1"
          label="Clear all"
          color="neutral"
          variant="ghost"
          size="xs"
          @click="clearFilters"
        />
      </div>

      <div class="min-h-0 flex-1">
        <UsersTable
          v-model:selected="selectedUserIds"
          :data="tableData"
          :loading="isLoading"
          @edit="openEdit"
          @restrict="openRestrict"
          @delete="openDelete"
          @clear-filters="clearFilters"
        />
      </div>

      <div
        v-if="userData && userData.totalCount > 0"
        class="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-gray-200/70 px-4 py-3 dark:border-gray-700/70 sm:px-6"
      >
        <USelect
          v-model="pageSize"
          :items="pageSizeOptions"
          aria-label="Users per page"
          size="sm"
          class="w-36"
        />

        <UPagination
          v-if="totalPages > 1"
          v-model:page="currentPage"
          :total="userData.totalCount"
          :items-per-page="uiState.pageSize"
          size="sm"
        />
      </div>
    </section>

    <!-- MODALS -->
    <CreateUserModal
      v-if="activeModal.type === 'create'"
      :open="true"
      :loading="isCreating"
      @close="closeModal"
      @submit="submitCreate"
    />
    <EditUserModal
      v-if="activeModal.type === 'edit'"
      :open="true"
      :user="activeModal.user"
      :loading="isUpdating"
      @close="closeModal"
      @submit="submitEdit"
    />
    <RestrictUserModal
      v-if="activeModal.type === 'restrict'"
      :open="true"
      :user="activeModal.user"
      :loading="isRestricting"
      @close="closeModal"
      @submit="submitRestrict"
    />
    <DeleteUsersModal
      v-if="activeModal.type === 'delete'"
      :open="true"
      :ids="activeModal.ids"
      :loading="isDeleting"
      @close="closeModal"
      @confirm="submitDelete"
    />
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { CreateUserSchema, RestrictUserSchema, UpdateUserSchema } from "@/schemas/user";
import type { UserDetailsDto } from "@/types/user";

import { userApi } from "@/api/users";
import DateRangeFilter from "@/components/common/DateRangeFilter.vue";
import CreateUserModal from "@/components/dashboard/users/CreateUserModal.vue";
import DeleteUsersModal from "@/components/dashboard/users/DeleteUserModal.vue";
import EditUserModal from "@/components/dashboard/users/EditUserModal.vue";
import RestrictUserModal from "@/components/dashboard/users/RestrictUserModal.vue";
import UsersTable from "@/components/dashboard/users/UsersTable.vue";
import { useAppToast } from "@/composables/useAppToast";
import {
  DATE_FILTERS,
  accountOptions,
  lockOptions,
  pageSizeOptions,
  roleOptions,
  sortByOptions,
  useUserRegistryFilters,
} from "@/features/users/registry-filters";
import { useCreateUser, useDeleteUsers, useRestrictUser, useUpdateUser } from "@/mutations/user";
import { USER_QUERY_KEYS } from "@/queries/user";
import { formatCount } from "@/utils/count";

const toast = useAppToast("users");

// Filters

const {
  accountsMode,
  activeFilterPills,
  advancedFilterCount,
  apiState,
  clearFilters,
  commitQuery,
  debouncedCommit,
  debouncedEmailCommit,
  emailDraft,
  emailError,
  getRange,
  isAscending,
  isDateFilterDisabled,
  lockMode,
  pageSize,
  roleFilter,
  setRange,
  sortBy,
  toggleSortDirection,
  uiState,
} = useUserRegistryFilters();

// Shared control shape with the media pages: 40px fields, 32px segments.
const fieldUi = { base: "h-10 rounded-xl" };

const isFilterPanelOpen = ref(false);

const sortDirectionIcon = computed(() => {
  if (isAscending.value) {
    return "mdi-sort-ascending";
  }
  return "mdi-sort-descending";
});

const sortDirectionLabel = computed(() => {
  if (isAscending.value) {
    return "Ascending";
  }
  return "Descending";
});

const sortDirectionTitle = computed(() => {
  if (isAscending.value) {
    return "Switch to descending";
  }
  return "Switch to ascending";
});

const isFilterButtonActive = computed(
  () => isFilterPanelOpen.value || advancedFilterCount.value > 0,
);

const filterButtonColor = computed(() => {
  if (isFilterButtonActive.value) {
    return "primary";
  }
  return "neutral";
});

const filterButtonVariant = computed(() => {
  if (isFilterButtonActive.value) {
    return "soft";
  }
  return "outline";
});

const filterPanelClass = computed(() => {
  if (isFilterPanelOpen.value) {
    return "grid-rows-[1fr] opacity-100 duration-200 ease-out";
  }
  return "grid-rows-[0fr] opacity-0 duration-150 ease-in";
});

const segmentColor = (active: boolean) => {
  if (active) {
    return "primary";
  }
  return "neutral";
};

const segmentVariant = (active: boolean) => {
  if (active) {
    return "soft";
  }
  return "ghost";
};

// Query

const {
  data: userData,
  isLoading,
  isPending,
} = useQuery({
  key: () => USER_QUERY_KEYS.getUsers(apiState.value),
  query: () => userApi.getUsers(apiState.value),
  staleTime: 30_000,
});

const tableData = computed(() => userData.value?.items ?? []);
const totalPages = computed(() =>
  userData.value ? Math.ceil(userData.value.totalCount / uiState.pageSize) : 1,
);
const currentPage = computed({
  get: () => uiState.page + 1,
  set: (v: number) => {
    uiState.page = v - 1;
    commitQuery();
  },
});

const resultSummary = computed(() => {
  if (isPending.value || !userData.value) {
    return "Loading users…";
  }

  const total = userData.value.totalCount;
  if (total === 0) {
    return "0 users";
  }

  const first = uiState.page * uiState.pageSize + 1;
  const last = Math.min(first + uiState.pageSize - 1, total);
  return `${first}–${last} of ${formatCount(total, "user")}`;
});

// Selection

const selectedUserIds = ref<string[]>([]);

// Mutations

const { mutateAsync: createUserMutation, isLoading: isCreating } = useCreateUser();
const { mutateAsync: updateUserMutation, isLoading: isUpdating } = useUpdateUser();
const { mutateAsync: restrictUserMutation, isLoading: isRestricting } = useRestrictUser();
const { mutateAsync: deleteUsersMutation, isLoading: isDeleting } = useDeleteUsers();

// Modal state

type ModalState =
  | { type: "none" }
  | { type: "create" }
  | { type: "edit"; user: UserDetailsDto }
  | { type: "restrict"; user: UserDetailsDto }
  | { type: "delete"; ids: string[] };

const activeModal = ref<ModalState>({ type: "none" });

const closeModal = () => (activeModal.value = { type: "none" });
const openCreate = () => (activeModal.value = { type: "create" });
const openEdit = (user: UserDetailsDto) => (activeModal.value = { type: "edit", user });
const openRestrict = (user: UserDetailsDto) => (activeModal.value = { type: "restrict", user });
const openDelete = (ids: string[]) => (activeModal.value = { ids, type: "delete" });

// Submit handlers

const submitCreate = async (data: CreateUserSchema) => {
  try {
    await createUserMutation(data);
    toast.success(`Created @${data.userName}`, "They'll receive an invitation to sign in.");
    closeModal();
  } catch (err) {
    toast.error(`Couldn't create @${data.userName}`, err);
  }
};

const submitEdit = async (data: UpdateUserSchema) => {
  if (activeModal.value.type !== "edit") {
    return;
  }
  const handle = `@${activeModal.value.user.userName}`;

  try {
    await updateUserMutation({ data, userId: activeModal.value.user.id });
    toast.success(`Saved changes to ${handle}`);
    closeModal();
  } catch (err) {
    toast.error(`Couldn't save changes to ${handle}`, err);
  }
};

const submitRestrict = async (data: RestrictUserSchema | { userId: string }) => {
  if (activeModal.value.type !== "restrict") {
    return;
  }

  const handle = `@${activeModal.value.user.userName}`;
  const isLifting = !("lockoutEndDate" in data);

  try {
    const lockoutEndDate =
      "lockoutEndDate" in data ? data.lockoutEndDate.toString() : new Date(0).toISOString();
    await restrictUserMutation({ lockoutEndDate, userId: data.userId });
    if (isLifting) {
      toast.success(`Lifted the restriction on ${handle}`);
    } else {
      toast.success(`Restricted ${handle}`, "They can't sign in until the restriction ends.");
    }
    closeModal();
  } catch (err) {
    toast.error(`Couldn't update the restriction on ${handle}`, err);
  }
};

const submitDelete = async () => {
  if (activeModal.value.type !== "delete") return;

  const { ids } = activeModal.value; // capture before any await

  try {
    await deleteUsersMutation(ids);
    toast.success(`Deleted ${formatCount(ids.length, "user")}`);
    selectedUserIds.value = selectedUserIds.value.filter((id) => !ids.includes(id));
    closeModal();
  } catch (err) {
    toast.error(`Couldn't delete ${formatCount(ids.length, "user")}`, err);
  }
};
</script>
