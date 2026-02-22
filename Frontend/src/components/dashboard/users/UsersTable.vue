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
    <!-- ══ Expanded row ══════════════════════════════════════════════════ -->
    <template #expanded="{ row }">
      <UserExpandedRow
        :user="row.original"
        @edit="$emit('edit', $event)"
        @restrict="$emit('restrict', $event)"
        @delete="$emit('delete', $event)"
      />
    </template>

    <!-- ══ Empty state ══════════════════════════════════════════════════ -->
    <template #empty>
      <div
        class="flex flex-col items-center justify-center py-20 text-center px-4"
      >
        <div class="rounded-full p-5 mb-4 bg-elevated border border-default">
          <UIcon name="i-lucide-users" class="size-9 text-muted" />
        </div>
        <p class="text-sm font-medium text-default mb-1">No users found</p>
        <p class="text-xs text-muted max-w-xs mb-4">
          No accounts match your current filters.
        </p>
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

function statusLabel(user: UserDetailsDto) {
  if (user.deletedAt) return "Deleted";
  if (user.isLockedOut) return "Locked";
  return "Active";
}

function statusColor(user: UserDetailsDto): "error" | "warning" | "success" {
  if (user.deletedAt) return "error";
  if (user.isLockedOut) return "warning";
  return "success";
}

function avatarBg(user: UserDetailsDto) {
  if (user.deletedAt)
    return "bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400";
  if (user.isLockedOut)
    return "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400";
  if (user.role === UserRole.Admin) return "bg-primary/15 text-primary";
  return "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300";
}

function rowActions(user: UserDetailsDto) {
  return [
    [
      {
        label: "Edit",
        icon: "i-lucide-pencil",
        onSelect: () => emit("edit", user),
      },
      {
        label: user.isLockedOut ? "Remove restriction" : "Restrict",
        icon: user.isLockedOut ? "i-lucide-lock-open" : "i-lucide-lock",
        onSelect: () => emit("restrict", user),
      },
    ],
    [
      {
        label: "Delete",
        icon: "i-lucide-trash-2",
        class: "text-error",
        onSelect: () => emit("delete", [user.id]),
      },
    ],
  ];
}

// Column definitions

const columns: TableColumn<UserDetailsDto>[] = [
  {
    id: "expand",
    header: "",
    cell: ({ row }) =>
      h(UButton, {
        color: "neutral",
        variant: "ghost",
        size: "xs",
        icon: row.getIsExpanded()
          ? "i-lucide-chevron-down"
          : "i-lucide-chevron-right",
        "aria-label": row.getIsExpanded() ? "Collapse row" : "Expand row",
        onClick: (e: MouseEvent) => {
          e.stopPropagation();
          row.toggleExpanded();
        },
      }),
    meta: { class: { th: "w-10", td: "w-10" } },
  },
  {
    id: "user",
    header: "User",
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
          h(
            "p",
            { class: "text-xs text-muted truncate leading-tight" },
            row.original.email,
          ),
        ]),
      ]),
    meta: { class: { th: "min-w-52", td: "min-w-52" } },
  },
  {
    id: "role",
    header: "Role",
    cell: ({ row }) =>
      h(
        UBadge,
        {
          color: row.original.role === UserRole.Admin ? "primary" : "neutral",
          variant: "subtle",
          size: "sm",
        },
        () => (row.original.role === UserRole.Admin ? "Admin" : "User"),
      ),
    meta: { class: { th: "w-24", td: "w-24" } },
  },
  {
    id: "status",
    header: "Status",
    cell: ({ row }) =>
      h(
        UBadge,
        { color: statusColor(row.original), variant: "subtle", size: "sm" },
        () => statusLabel(row.original),
      ),
    meta: { class: { th: "w-28", td: "w-28" } },
  },
  {
    accessorKey: "createdAt",
    header: "Created",
    cell: ({ row }) =>
      h(
        "span",
        { class: "text-sm text-muted tabular-nums" },
        formatDate(row.original.createdAt),
      ),
    meta: { class: { th: "w-32", td: "w-32" } },
  },
  {
    id: "actions",
    header: "",
    cell: ({ row }) =>
      h("div", { class: "flex items-center justify-end gap-1" }, [
        h(UButton, {
          color: "neutral",
          variant: "ghost",
          size: "xs",
          icon: "i-lucide-pencil",
          "aria-label": "Edit user",
          onClick: (e: MouseEvent) => {
            e.stopPropagation();
            emit("edit", row.original);
          },
        }),
        h(
          UDropdownMenu,
          { items: rowActions(row.original), ui: { content: "min-w-40" } },
          () =>
            h(UButton, {
              color: "neutral",
              variant: "ghost",
              size: "xs",
              icon: "i-lucide-ellipsis-vertical",
              "aria-label": "More actions",
              onClick: (e: MouseEvent) => e.stopPropagation(),
            }),
        ),
      ]),
    meta: { class: { th: "w-20 text-right", td: "w-20" } },
  },
];
</script>
