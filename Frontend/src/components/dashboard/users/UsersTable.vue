<template>
  <UTable
    :model-value="selected"
    :data="data"
    :columns="columns"
    :loading="loading"
    loading-color="primary"
    loading-animation="carousel"
    sticky
    class="h-full"
    by="id"
    @update:model-value="$emit('update:selected', $event)"
  >
    <!-- Expanded row -->
    <template #expanded="{ row }">
      <UserExpandedRow
        :user="row.original"
        @edit="$emit('edit', $event)"
        @restrict="$emit('restrict', $event)"
        @delete="$emit('delete', $event)"
      />
    </template>

    <!-- Empty state -->
    <template #empty>
      <div class="flex flex-col items-center justify-center py-20 text-center px-4">
        <div class="rounded-full p-5 mb-4 bg-elevated border border-default">
          <UIcon name="i-lucide-users" class="size-9 text-muted" />
        </div>
        <p class="text-sm font-medium text-default mb-1">No users found</p>
        <p class="text-xs text-muted max-w-xs mb-4">No accounts match your current filters.</p>
        <UButton
          size="sm"
          color="neutral"
          variant="outline"
          icon="i-lucide-rotate-ccw"
          label="Clear filters"
          @click="$emit('clear-filters')"
        />
      </div>
    </template>
  </UTable>
</template>

<script setup lang="ts">
import { h, resolveComponent } from "vue";
import type { TableColumn } from "@nuxt/ui";
import { UserRole } from "@/enums/UserRole";
import type { UserDetailsDto } from "@/types/user";
import UserExpandedRow from "@/components/dashboard/users/UserExpandedRow.vue";
import { formatDate } from "@/utils/date-formatters";

// Props & emits

defineProps<{
  data: UserDetailsDto[];
  loading: boolean;
  selected: string[];
}>();

const emit = defineEmits<{
  "update:selected": [ids: string[]];
  edit: [user: UserDetailsDto];
  restrict: [user: UserDetailsDto];
  delete: [ids: string[]];
  "clear-filters": [];
}>();

// Resolved components (needed for render functions)

const UButton = resolveComponent("UButton");
const UBadge = resolveComponent("UBadge");
const UDropdownMenu = resolveComponent("UDropdownMenu");

// Cell helpers

const statusLabel = (user: UserDetailsDto) => {
  if (user.deletedAt) {
    return "Deleted";
  }
  if (user.isLockedOut) {
    return "Locked";
  }
  return "Active";
};

const statusColor = (user: UserDetailsDto): "error" | "warning" | "success" => {
  if (user.deletedAt) {
    return "error";
  }
  if (user.isLockedOut) {
    return "warning";
  }
  return "success";
};

const avatarBg = (user: UserDetailsDto) => {
  if (user.deletedAt) {
    return "bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400";
  }
  if (user.isLockedOut) {
    return "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400";
  }
  if (user.role === UserRole.Admin) {
    return "bg-primary/15 text-primary";
  }
  return "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300";
};

const rowActions = (user: UserDetailsDto) => [
  [
    {
      icon: "i-lucide-pencil",
      label: "Edit",
      onSelect: () => emit("edit", user),
    },
    {
      icon: user.isLockedOut ? "i-lucide-lock-open" : "i-lucide-lock",
      label: user.isLockedOut ? "Remove restriction" : "Restrict",
      onSelect: () => emit("restrict", user),
    },
  ],
  [
    {
      class: "text-error",
      icon: "i-lucide-trash-2",
      label: "Delete",
      onSelect: () => emit("delete", [user.id]),
    },
  ],
];

// Column definitions

const columns: TableColumn<UserDetailsDto>[] = [
  {
    cell: ({ row }) =>
      h(UButton, {
        "aria-label": row.getIsExpanded() ? "Collapse row" : "Expand row",
        color: "neutral",
        icon: row.getIsExpanded() ? "i-lucide-chevron-down" : "i-lucide-chevron-right",
        onClick: (e: MouseEvent) => {
          e.stopPropagation();
          row.toggleExpanded();
        },
        size: "xs",
        variant: "ghost",
      }),
    header: "",
    id: "expand",
    meta: { class: { td: "w-10", th: "w-10" } },
  },
  {
    cell: ({ row }) =>
      h("div", { class: "flex items-center gap-2.5 min-w-0" }, [
        h(
          "div",
          {
            class: `flex-shrink-0 w-7 h-7 rounded-full flex items-center justify-center text-xs font-semibold ${avatarBg(row.original)}`,
          },
          row.original.userName?.charAt(0)?.toUpperCase() ?? "?",
        ),
        h("div", { class: "min-w-0" }, [
          h(
            "p",
            {
              class: "text-sm font-medium text-default truncate leading-tight",
            },
            row.original.userName,
          ),
          h("p", { class: "text-xs text-muted truncate leading-tight" }, row.original.email),
        ]),
      ]),
    header: "User",
    id: "user",
    meta: { class: { td: "min-w-52", th: "min-w-52" } },
  },
  {
    cell: ({ row }) =>
      h(
        UBadge,
        {
          color: row.original.role === UserRole.Admin ? "primary" : "neutral",
          size: "sm",
          variant: "subtle",
        },
        () => (row.original.role === UserRole.Admin ? "Admin" : "User"),
      ),
    header: "Role",
    id: "role",
    meta: { class: { td: "w-24", th: "w-24" } },
  },
  {
    cell: ({ row }) =>
      h(UBadge, { color: statusColor(row.original), size: "sm", variant: "subtle" }, () =>
        statusLabel(row.original),
      ),
    header: "Status",
    id: "status",
    meta: { class: { td: "w-28", th: "w-28" } },
  },
  {
    accessorKey: "createdAt",
    cell: ({ row }) =>
      h("span", { class: "text-sm text-muted tabular-nums" }, formatDate(row.original.createdAt)),
    header: "Created",
    meta: { class: { td: "w-32", th: "w-32" } },
  },
  {
    cell: ({ row }) =>
      h("div", { class: "flex items-center justify-end gap-1" }, [
        h(UButton, {
          "aria-label": "Edit user",
          color: "neutral",
          icon: "i-lucide-pencil",
          onClick: (e: MouseEvent) => {
            e.stopPropagation();
            emit("edit", row.original);
          },
          size: "xs",
          variant: "ghost",
        }),
        h(UDropdownMenu, { items: rowActions(row.original), ui: { content: "min-w-40" } }, () =>
          h(UButton, {
            "aria-label": "More actions",
            color: "neutral",
            icon: "i-lucide-ellipsis-vertical",
            onClick: (e: MouseEvent) => e.stopPropagation(),
            size: "xs",
            variant: "ghost",
          }),
        ),
      ]),
    header: "",
    id: "actions",
    meta: { class: { td: "w-20", th: "w-20 text-right" } },
  },
];
</script>
