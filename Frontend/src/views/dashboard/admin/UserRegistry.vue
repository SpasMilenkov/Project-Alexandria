<template>
  <div class="flex flex-col h-full min-h-0 gap-4 p-4 sm:p-6">
    <!-- PAGE HEADER  -->
    <div class="flex items-start justify-between gap-4">
      <div>
        <h1 class="text-xl font-semibold text-default">User Registry</h1>
        <p class="text-sm text-muted mt-0.5">
          Manage accounts, roles, and access restrictions.
        </p>
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

    <!--  TOOLBAR  -->
    <div
      class="flex flex-wrap items-center gap-2 rounded-lg border border-default px-3 py-2 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
    >
      <UInput
        v-model="uiState.userName"
        placeholder="Search by username…"
        icon="i-lucide-search"
        size="sm"
        class="w-52"
        @update:model-value="debouncedCommit"
      />

      <USeparator orientation="vertical" class="h-5 hidden sm:block" />

      <UTooltip text="Show only locked-out accounts" :delay-duration="300">
        <UButton
          size="xs"
          :color="uiState.isLockedOut === true ? 'warning' : 'neutral'"
          :variant="uiState.isLockedOut === true ? 'soft' : 'outline'"
          icon="i-lucide-lock"
          label="Locked"
          @click="toggleQuick('locked')"
        />
      </UTooltip>

      <UTooltip
        text="Include soft-deleted accounts in results"
        :delay-duration="300"
      >
        <UButton
          size="xs"
          :color="uiState.showDeleted ? 'error' : 'neutral'"
          :variant="uiState.showDeleted ? 'soft' : 'outline'"
          icon="i-lucide-trash"
          label="Show deleted"
          @click="toggleQuick('deleted')"
        />
      </UTooltip>

      <UTooltip text="Show only deleted accounts" :delay-duration="300">
        <UButton
          size="xs"
          :color="uiState.showDeletedOnly ? 'error' : 'neutral'"
          :variant="uiState.showDeletedOnly ? 'solid' : 'outline'"
          icon="i-lucide-archive"
          label="Deleted only"
          @click="toggleQuick('deletedOnly')"
        />
      </UTooltip>

      <div class="ml-auto flex items-center gap-2">
        <span v-if="!isLoading && data" class="text-xs text-muted">
          {{ data.totalCount.toLocaleString() }} user{{
            data.totalCount !== 1 ? "s" : ""
          }}
        </span>
        <USeparator orientation="vertical" class="h-5" />
        <UTooltip text="Toggle advanced filters" :delay-duration="300">
          <UButton
            size="sm"
            :color="isFilterPanelOpen ? 'primary' : 'neutral'"
            :variant="isFilterPanelOpen ? 'soft' : 'outline'"
            icon="i-lucide-sliders-horizontal"
            label="Filters"
            @click="isFilterPanelOpen = !isFilterPanelOpen"
          />
        </UTooltip>
      </div>
    </div>

    <!-- FILTER PANE -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-2"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition-all duration-150 ease-in"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 -translate-y-2"
    >
      <div
        v-if="isFilterPanelOpen"
        class="rounded-xl border border-default bg-white/70 dark:bg-white/5 backdrop-blur-sm overflow-hidden max-h-[40vh] overflow-y-auto shrink-0"
      >
        <UForm
          ref="filterForm"
          :schema="userQueryUiSchema"
          :state="uiState"
          @submit="applyFilters"
        >
          <div
            class="flex items-center justify-between px-5 py-3 border-b border-default bg-elevated/30"
          >
            <span class="text-sm font-semibold text-default"
              >Advanced Filters</span
            >
            <div class="flex items-center gap-2">
              <UButton
                type="button"
                size="xs"
                color="neutral"
                variant="ghost"
                icon="i-lucide-rotate-ccw"
                label="Reset"
                @click="clearFilters()"
              />
              <UButton
                size="xs"
                color="primary"
                variant="soft"
                icon="i-lucide-check"
                label="Apply"
                @click="filterForm?.submit()"
              />
            </div>
          </div>

          <div class="divide-y divide-default">
            <!-- Identity & status -->
            <div class="px-5 py-4">
              <p
                class="text-xs font-semibold uppercase tracking-wider text-muted mb-3 flex items-center gap-1.5"
              >
                <UIcon name="i-lucide-user-search" class="size-3.5" />
                Identity &amp; status
              </p>
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <UFormField label="Username" name="userName">
                  <UInput
                    v-model="uiState.userName"
                    placeholder="Contains…"
                    icon="i-lucide-user"
                    class="w-full"
                  />
                </UFormField>
                <UFormField label="Email" name="userEmail">
                  <UInput
                    v-model="uiState.userEmail"
                    placeholder="Contains…"
                    icon="i-lucide-mail"
                    class="w-full"
                  />
                </UFormField>
                <UFormField label="Role" name="role">
                  <USelect
                    v-model="uiState.role"
                    :items="[
                      { label: 'Any role', value: null },
                      ...roleOptions,
                    ]"
                    class="w-full"
                  />
                </UFormField>
                <UFormField label="Lock status" name="isLockedOut">
                  <USelect
                    v-model="uiState.isLockedOut"
                    :items="lockStatusOptions"
                    class="w-full"
                  />
                </UFormField>
              </div>
            </div>

            <!-- Deleted accounts -->
            <div class="px-5 py-4">
              <p
                class="text-xs font-semibold uppercase tracking-wider text-muted mb-3 flex items-center gap-1.5"
              >
                <UIcon name="i-lucide-trash-2" class="size-3.5" />
                Deleted accounts
                <UTooltip
                  text="'Include deleted' and 'Deleted only' are mutually exclusive."
                  :delay-duration="200"
                >
                  <UIcon
                    name="i-lucide-info"
                    class="size-3 text-muted/60 cursor-help"
                  />
                </UTooltip>
              </p>
              <div class="flex flex-wrap gap-6">
                <UFormField name="showDeleted">
                  <UCheckbox
                    v-model="uiState.showDeleted"
                    label="Include deleted"
                    description="Mix deleted accounts into results alongside active ones."
                    @update:model-value="
                      (v) => {
                        if (v) uiState.showDeletedOnly = false;
                      }
                    "
                  />
                </UFormField>
                <UFormField name="showDeletedOnly">
                  <UCheckbox
                    v-model="uiState.showDeletedOnly"
                    label="Deleted only"
                    description="Show exclusively deleted accounts, hiding all active ones."
                    @update:model-value="
                      (v) => {
                        if (v) uiState.showDeleted = false;
                      }
                    "
                  />
                </UFormField>
              </div>
            </div>

            <!-- Sorting & pagination -->
            <div class="px-5 py-4">
              <p
                class="text-xs font-semibold uppercase tracking-wider text-muted mb-3 flex items-center gap-1.5"
              >
                <UIcon name="i-lucide-arrow-up-down" class="size-3.5" />
                Sorting &amp; pagination
              </p>
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <UFormField label="Sort by" name="sortBy">
                  <USelect
                    v-model="uiState.sortBy"
                    :items="sortByOptions"
                    class="w-full"
                  />
                </UFormField>
                <UFormField label="Direction" name="sortDirection">
                  <USelect
                    v-model="uiState.sortDirection"
                    :items="sortDirectionOptions"
                    class="w-full"
                  />
                </UFormField>
                <UFormField label="Page size" name="pageSize">
                  <USelect
                    v-model="uiState.pageSize"
                    :items="pageSizeOptions"
                    class="w-full"
                  />
                </UFormField>
              </div>
            </div>

            <!-- Date ranges (collapsible) -->
            <UCollapsible v-model:open="isDateRangesOpen">
              <div
                class="px-5 py-3 flex items-center justify-between cursor-pointer select-none hover:bg-elevated/30 transition-colors"
              >
                <p
                  class="text-xs font-semibold uppercase tracking-wider text-muted flex items-center gap-1.5"
                >
                  <UIcon name="i-lucide-calendar-range" class="size-3.5" />
                  Date ranges
                </p>
                <UIcon
                  :name="
                    isDateRangesOpen
                      ? 'i-lucide-chevron-up'
                      : 'i-lucide-chevron-down'
                  "
                  class="size-4 text-muted transition-transform duration-200"
                />
              </div>

              <template #content>
                <div class="px-5 pb-5 pt-2 space-y-4">
                  <!-- Created -->
                  <div>
                    <p
                      class="text-xs text-muted font-medium mb-2 flex items-center gap-1"
                    >
                      <UIcon name="i-lucide-calendar-plus" class="size-3" />
                      Created
                    </p>
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <UFormField
                        label="After"
                        name="createdAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="createdAfter"
                          v-model="uiState.createdAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="createdAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.createdAfter"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                      <UFormField
                        label="Before"
                        name="createdBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="createdBefore"
                          v-model="uiState.createdBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="createdBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.createdBefore"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                    </div>
                  </div>

                  <USeparator />

                  <!-- Updated -->
                  <div>
                    <p
                      class="text-xs text-muted font-medium mb-2 flex items-center gap-1"
                    >
                      <UIcon name="i-lucide-calendar-clock" class="size-3" />
                      Updated
                    </p>
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <UFormField
                        label="After"
                        name="updatedAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="updatedAfter"
                          v-model="uiState.updatedAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="updatedAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.updatedAfter"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                      <UFormField
                        label="Before"
                        name="updatedBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="updatedBefore"
                          v-model="uiState.updatedBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="updatedBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.updatedBefore"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                    </div>
                  </div>

                  <USeparator />

                  <!-- Deleted -->
                  <div>
                    <p
                      class="text-xs text-muted font-medium mb-2 flex items-center gap-1"
                    >
                      <UIcon name="i-lucide-calendar-x" class="size-3" />
                      Deleted
                    </p>
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <UFormField
                        label="After"
                        name="deletedAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="deletedAfter"
                          v-model="uiState.deletedAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="deletedAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.deletedAfter"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                      <UFormField
                        label="Before"
                        name="deletedBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="deletedBefore"
                          v-model="uiState.deletedBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="deletedBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.deletedBefore"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                    </div>
                  </div>

                  <USeparator />

                  <!-- Locked out -->
                  <div>
                    <p
                      class="text-xs text-muted font-medium mb-2 flex items-center gap-1"
                    >
                      <UIcon name="i-lucide-lock" class="size-3" /> Locked out
                    </p>
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <UFormField
                        label="After"
                        name="lockedOutAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="lockedOutAfter"
                          v-model="uiState.lockedOutAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="lockedOutAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.lockedOutAfter"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                      <UFormField
                        label="Before"
                        name="lockedOutBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="lockedOutBefore"
                          v-model="uiState.lockedOutBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="lockedOutBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Pick date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar
                                  v-model="uiState.lockedOutBefore"
                                  class="p-2"
                                />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                    </div>
                  </div>
                </div>
              </template>
            </UCollapsible>
          </div>
        </UForm>
      </div>
    </Transition>

    <!-- TABLE  -->
    <div
      class="flex-1 min-h-0 rounded-xl border border-default overflow-x-hidden bg-white/60 dark:bg-white/5 backdrop-blur-sm"
    >
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

    <!-- PAGINATION -->
    <div
      v-if="data && data.totalCount > uiState.pageSize"
      class="flex items-center justify-between"
    >
      <span class="text-xs text-muted"
        >Page {{ uiState.page + 1 }} of {{ totalPages }}</span
      >
      <UPagination
        v-model:page="currentPage"
        :total="data.totalCount"
        :items-per-page="uiState.pageSize"
        size="sm"
      />
    </div>

    <!-- MODALS -->
    <CreateUserModal
      :open="activeModal.type === 'create'"
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
import { reactive, ref, computed, shallowRef } from "vue";
import { useQuery } from "@pinia/colada";
import { userQueryUiSchema, userQueryApiSchema } from "@/schemas/user";
import { UserRole } from "@/enums/UserRole";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { userApi } from "@/api/users";
import type { UserDetailsDto } from "@/types/user";
import type {
  UpdateUserSchema,
  RestrictUserSchema,
  CreateUserSchema,
} from "@/schemas/user";
import { USER_QUERY_KEYS } from "@/queries/user";
import { useDebounceFn } from "@vueuse/core";
import UsersTable from "@/components/dashboard/users/UsersTable.vue";
import EditUserModal from "@/components/dashboard/users/EditUserModal.vue";
import RestrictUserModal from "@/components/dashboard/users/RestrictUserModal.vue";
import DeleteUsersModal from "@/components/dashboard/users/DeleteUserModal.vue";
import CreateUserModal from "@/components/dashboard/users/CreateUserModal.vue";
import {
  useUpdateUser,
  useRestrictUser,
  useDeleteUsers,
  useCreateUser,
} from "@/mutations/user";

const toast = useToast();

// Filter state
const createdAfter = shallowRef();
const createdBefore = shallowRef();
const updatedAfter = shallowRef();
const updatedBefore = shallowRef();
const deletedAfter = shallowRef();
const deletedBefore = shallowRef();
const lockedOutAfter = shallowRef();
const lockedOutBefore = shallowRef();
const uiState = reactive(userQueryUiSchema.parse({}));
const apiState = ref(userQueryApiSchema.parse(uiState));
const isFilterPanelOpen = ref(false);
const isDateRangesOpen = ref(false);
const filterForm = ref();

function commitQuery() {
  apiState.value = userQueryApiSchema.parse(uiState);
}

const debouncedCommit = useDebounceFn(() => {
  uiState.page = 0;
  commitQuery();
}, 350);

function applyFilters() {
  uiState.page = 0;
  commitQuery();
}

function clearFilters() {
  const defaults = userQueryUiSchema.parse({});
  (Object.keys(uiState) as (keyof typeof uiState)[]).forEach((key) => {
    (uiState as Record<string, unknown>)[key] = (
      defaults as Record<string, unknown>
    )[key];
  });
  commitQuery();
}

function toggleQuick(kind: "locked" | "deleted" | "deletedOnly") {
  if (kind === "locked")
    uiState.isLockedOut = uiState.isLockedOut === true ? null : true;
  if (kind === "deleted") {
    uiState.showDeleted = !uiState.showDeleted;
    if (uiState.showDeleted) uiState.showDeletedOnly = false;
  }
  if (kind === "deletedOnly") {
    uiState.showDeletedOnly = !uiState.showDeletedOnly;
    if (uiState.showDeletedOnly) uiState.showDeleted = false;
  }
  uiState.page = 0;
  commitQuery();
}

// Query

const { data, isLoading } = useQuery({
  key: () => USER_QUERY_KEYS.getUsers(apiState.value),
  query: () => userApi.getUsers(apiState.value),
  staleTime: 30_000,
});

const tableData = computed(() => data.value?.items ?? []);
const totalPages = computed(() =>
  data.value ? Math.ceil(data.value.totalCount / uiState.pageSize) : 1,
);
const currentPage = computed({
  get: () => uiState.page + 1,
  set: (v: number) => {
    uiState.page = v - 1;
    commitQuery();
  },
});

// Selection

const selectedUserIds = ref<string[]>([]);

// Mutations

const { mutateAsync: createUserMutation } = useCreateUser();
const { mutateAsync: updateUserMutation, isLoading: isUpdating } =
  useUpdateUser();
const { mutateAsync: restrictUserMutation, isLoading: isRestricting } =
  useRestrictUser();
const { mutateAsync: deleteUsersMutation, isLoading: isDeleting } =
  useDeleteUsers();

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
const openEdit = (user: UserDetailsDto) =>
  (activeModal.value = { type: "edit", user });
const openRestrict = (user: UserDetailsDto) =>
  (activeModal.value = { type: "restrict", user });
const openDelete = (ids: string[]) =>
  (activeModal.value = { type: "delete", ids });

// Submit handlers

async function submitCreate(data: CreateUserSchema) {
  try {
    await createUserMutation(data);
    toast.add({
      title: "Account created",
      description: `@${data.userName} has been added and will receive an invitation.`,
      color: "success",
      icon: "i-lucide-user-check",
    });
    closeModal();
  } catch {
    toast.add({
      title: "Could not create account",
      description: "Please check the details and try again.",
      color: "error",
      icon: "i-lucide-x-circle",
    });
  }
}

async function submitEdit(data: UpdateUserSchema) {
  if (activeModal.value.type !== "edit") return;
  try {
    await updateUserMutation({ userId: activeModal.value.user.id, data });
    toast.add({
      title: "User updated",
      color: "success",
      icon: "i-lucide-check-circle",
    });
    closeModal();
  } catch {
    toast.add({
      title: "Update failed",
      color: "error",
      icon: "i-lucide-x-circle",
    });
  }
}

async function submitRestrict(data: RestrictUserSchema | { userId: string }) {
  if (activeModal.value.type !== "restrict") return;
  try {
    const lockoutEndDate =
      "lockoutEndDate" in data
        ? data.lockoutEndDate.toString()
        : new Date(0).toISOString();
    await restrictUserMutation({ userId: data.userId, lockoutEndDate });
    toast.add({
      title: "Restriction updated",
      color: "warning",
      icon: "i-lucide-lock",
    });
    closeModal();
  } catch {
    toast.add({
      title: "Restriction failed",
      color: "error",
      icon: "i-lucide-x-circle",
    });
  }
}

async function submitDelete() {
  if (activeModal.value.type !== "delete") return;
  try {
    await deleteUsersMutation(activeModal.value.ids);
    toast.add({
      title: `${activeModal.value.ids.length} user(s) deleted`,
      color: "success",
      icon: "i-lucide-check-circle",
    });
    selectedUserIds.value = selectedUserIds.value.filter(
      (id) => !activeModal.value.ids.includes(id),
    );
    closeModal();
  } catch {
    toast.add({
      title: "Delete failed",
      color: "error",
      icon: "i-lucide-x-circle",
    });
  }
}

// Select options

const roleOptions = [
  {
    label: "User",
    value: UserRole.User,
    icon: "i-lucide-user",
    description: "Standard access.",
  },
  {
    label: "Administrator",
    value: UserRole.Admin,
    icon: "i-lucide-shield-check",
    description: "Full access.",
  },
];

const lockStatusOptions = [
  { label: "All accounts", value: null },
  { label: "Active only", value: false },
  { label: "Locked only", value: true },
];

const sortByOptions = [
  { label: "Name", value: SortBy.Name },
  { label: "Created", value: SortBy.CreatedAt },
  { label: "Last updated", value: SortBy.UpdatedAt },
  { label: "File count", value: SortBy.FileCount },
  { label: "Storage used", value: SortBy.TotalSize },
];

const sortDirectionOptions = [
  { label: "Newest first", value: SortDirection.Desc },
  { label: "Oldest first", value: SortDirection.Asc },
];

const pageSizeOptions = [
  { label: "10 per page", value: 10 },
  { label: "20 per page", value: 20 },
  { label: "50 per page", value: 50 },
  { label: "100 per page", value: 100 },
];
</script>
