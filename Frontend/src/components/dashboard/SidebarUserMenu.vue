<template>
  <UPopover v-model:open="open" :content="menuContent" :ui="{ content: 'w-72 p-0' }">
    <UButton
      color="neutral"
      variant="ghost"
      block
      :square="collapsed"
      :aria-label="`Account menu for ${userName}`"
      class="gap-2.5 data-[state=open]:bg-elevated/60"
      :class="collapsed ? 'justify-center p-1' : 'justify-start px-2 py-1.5'"
    >
      <UAvatar :alt="userName" :size="collapsed ? 'xs' : 'sm'" />

      <template v-if="!collapsed">
        <span class="flex min-w-0 flex-1 flex-col text-left leading-tight">
          <span class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
            {{ userName }}
          </span>
          <span
            v-if="subtitle"
            class="truncate text-xs font-normal text-gray-500 dark:text-gray-500"
          >
            {{ subtitle }}
          </span>
        </span>

        <UIcon
          name="i-lucide-chevrons-up-down"
          class="size-4 shrink-0 text-gray-500 dark:text-gray-500"
        />
      </template>
    </UButton>

    <template #content>
      <div class="flex flex-col">
        <div class="relative" @mouseleave="endHover">
          <div
            :id="detailsId"
            class="absolute inset-x-0 bottom-full z-10 pb-2 transition-[opacity,translate] motion-reduce:transition-none"
            :class="detailsClass"
            :inert="!detailsOpen"
          >
            <div
              class="flex flex-col gap-1.5 rounded-md bg-default px-3 py-2.5 shadow-lg ring ring-default"
            >
              <div class="flex items-center justify-between gap-2 text-xs">
                <span class="font-medium text-gray-900 dark:text-gray-100">
                  Storage breakdown
                </span>

                <button
                  type="button"
                  class="flex items-center gap-0.5 rounded-sm text-gray-600 transition-colors hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400 dark:hover:text-gray-100"
                  @click="go('/my-storage')"
                >
                  Manage
                  <UIcon name="i-lucide-chevron-right" class="size-3.5" />
                </button>
              </div>

              <ul class="flex flex-col text-xs">
                <li
                  v-for="row in breakdown"
                  :key="row.key"
                  class="-mx-1.5 grid grid-cols-[minmax(0,1fr)_auto_2.5rem] items-center gap-3 rounded-md px-1.5 py-1 transition-colors"
                  :class="{ 'bg-elevated': activeKey === row.key }"
                  @mouseenter="activeKey = row.key"
                  @mouseleave="activeKey = null"
                >
                  <span class="flex min-w-0 items-center gap-2 text-gray-700 dark:text-gray-300">
                    <span class="size-2 shrink-0 rounded-full" :class="row.dot" />
                    <span class="truncate">{{ row.label }}</span>
                  </span>
                  <span class="text-gray-600 tabular-nums dark:text-gray-400">
                    {{ formatBytes(row.bytes) }}
                  </span>
                  <span class="text-right text-gray-500 tabular-nums dark:text-gray-500">
                    {{ row.percent }}
                  </span>
                </li>
              </ul>
            </div>
          </div>

          <div class="flex items-center gap-3 px-4 pt-4 pb-2">
            <button
              type="button"
              class="shrink-0 rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
              :aria-expanded="detailsOpen"
              :aria-controls="detailsId"
              aria-label="Storage breakdown"
              @mouseenter="hovering = true"
              @click="pinned = !pinned"
            >
              <StorageRing
                :arcs="arcs"
                :active-key="activeKey"
                :emphasized="detailsOpen"
                @hover-segment="activeKey = $event"
              >
                <UAvatar :alt="userName" size="lg" />
              </StorageRing>
            </button>

            <div class="flex min-w-0 flex-1 flex-col leading-tight">
              <span class="flex min-w-0 items-center gap-1.5">
                <span class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
                  {{ userName }}
                </span>
                <UBadge
                  v-if="authStore.isAdmin"
                  label="Admin"
                  color="neutral"
                  variant="subtle"
                  size="xs"
                />
              </span>
              <span v-if="userEmail" class="truncate text-xs text-gray-500 dark:text-gray-500">
                {{ userEmail }}
              </span>
            </div>
          </div>

          <button
            type="button"
            class="mx-2 flex w-[calc(100%-1rem)] items-center justify-between gap-2 rounded-lg px-2 py-2 text-sm transition-colors hover:bg-black/4 focus-visible:outline-2 focus-visible:outline-primary dark:hover:bg-white/5"
            :aria-expanded="detailsOpen"
            :aria-controls="detailsId"
            @mouseenter="hovering = true"
            @click="pinned = !pinned"
          >
            <span class="text-gray-600 dark:text-gray-400">Storage</span>

            <span class="flex min-w-0 items-center gap-2">
              <UIcon
                v-if="isPending"
                name="i-mdi-loading"
                class="size-4 animate-spin text-gray-500 dark:text-gray-500"
              />
              <span v-else class="truncate text-gray-900 tabular-nums dark:text-gray-100">
                {{ usageLabel }}
              </span>
              <UIcon
                name="i-lucide-chevron-down"
                class="size-4 shrink-0 text-gray-500 transition-transform duration-200 ease-out dark:text-gray-500"
                :class="{ 'rotate-180': detailsOpen }"
              />
            </span>
          </button>
        </div>

        <div class="px-2 pt-2 pb-2">
          <div
            class="grid grid-cols-3 gap-1 rounded-lg border border-gray-200/70 bg-elevated/40 p-1 dark:border-gray-700/70"
          >
            <button
              v-for="tile in tiles"
              :key="tile.label"
              type="button"
              class="flex min-w-0 flex-col items-center gap-1.5 rounded-md px-1 py-2.5 text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-primary"
              :class="tileClass(tile.active)"
              :aria-current="tile.active ? 'page' : undefined"
              @click="tile.select"
            >
              <UIcon :name="tile.icon" class="size-5" />
              <span class="max-w-full truncate">{{ tile.label }}</span>
            </button>
          </div>
        </div>

        <div class="border-t border-gray-200/70 px-2 py-1.5 dark:border-gray-700/70">
          <button
            type="button"
            class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-sm text-gray-600 transition-colors hover:bg-black/4 hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-gray-100"
            @click="logOut"
          >
            <UIcon name="i-lucide-log-out" class="size-4" />
            Log out
          </button>
        </div>
      </div>
    </template>
  </UPopover>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, useId, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import type { StorageBreakdown } from "@/api/status";

import StorageRing from "@/components/dashboard/StorageRing.vue";
import { myStorage } from "@/queries/status";
import { useAuthStore } from "@/stores/auth";
import { formatBytes } from "@/utils/size.utils";
import { type RingArc, buildRingArcs } from "@/utils/storage-ring";

const { collapsed = false } = defineProps<{ collapsed?: boolean }>();

const emit = defineEmits<{
  logout: [];
  reportProblem: [];
}>();

const SEGMENTS = [
  {
    color: "var(--ui-bg-inverted)",
    dot: "bg-inverted",
    field: "filesSize",
    key: "files",
    label: "Files",
  },
  {
    color: "var(--ui-info)",
    dot: "bg-info",
    field: "previewsSize",
    key: "previews",
    label: "Previews",
  },
  {
    color: "var(--ui-success)",
    dot: "bg-success",
    field: "transcodedSize",
    key: "transcoded",
    label: "Transcoded",
  },
] as const satisfies readonly {
  color: string;
  dot: string;
  field: keyof StorageBreakdown;
  key: string;
  label: string;
}[];

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const { data, error, isPending } = useQuery(myStorage);

const detailsId = useId();
const open = ref(false);
const hovering = ref(false);
const pinned = ref(false);
const activeKey = ref<string | null>(null);

const detailsOpen = computed(() => hovering.value || pinned.value);

watch(open, (isOpen) => {
  if (isOpen) return;

  hovering.value = false;
  pinned.value = false;
  activeKey.value = null;
});

// The breakdown floats above the card instead of growing it: resizing the card made the popover
// re-anchor a frame late on every animation step, which read as trembling. Only opacity and
// translate animate, so the card never changes size. The bottom padding bridges the gap to the
// card so moving the pointer up into the breakdown does not end the hover.
const detailsClass = computed(() => {
  if (detailsOpen.value) return "translate-y-0 opacity-100 duration-200 ease-out";

  return "pointer-events-none translate-y-1 opacity-0 duration-150 ease-in";
});

const endHover = () => {
  hovering.value = false;
  activeKey.value = null;
};

const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

const storageError = computed(() => Boolean(error.value) && !isAuthError(error.value));

const userName = computed(() => authStore.user?.user.name || "Account");
const userEmail = computed(() => authStore.user?.user.email ?? "");

const quotaBytes = computed(() => data.value?.quotaBytes ?? 0);
const usedBytes = computed(() => data.value?.usedBytes ?? 0);
const isUnlimited = computed(() => quotaBytes.value <= 0);
const remainingBytes = computed(() => quotaBytes.value - usedBytes.value);

const usagePercent = computed(() => {
  if (isUnlimited.value) return 0;

  return Math.min(100, (usedBytes.value / quotaBytes.value) * 100);
});

const usageLabel = computed(() => {
  if (storageError.value) return "Unavailable";
  if (isUnlimited.value) return `${formatBytes(usedBytes.value)} used`;

  return `${formatBytes(usedBytes.value)} / ${formatBytes(quotaBytes.value)}`;
});

const formatShare = (bytes: number, base: number) => {
  if (base <= 0 || bytes <= 0) return "0%";

  const share = (bytes / base) * 100;
  if (share < 1) return "<1%";

  return `${Math.round(share)}%`;
};

interface BreakdownRow {
  bytes: number;
  dot: string;
  key: string;
  label: string;
  percent: string;
}

const breakdown = computed<BreakdownRow[]>(() => {
  const storage = data.value;
  if (!storage) return [];

  const base = isUnlimited.value ? usedBytes.value : quotaBytes.value;
  const rows: BreakdownRow[] = SEGMENTS.map((segment) => ({
    bytes: storage[segment.field],
    dot: segment.dot,
    key: segment.key,
    label: segment.label,
    percent: formatShare(storage[segment.field], base),
  }));

  if (!isUnlimited.value && remainingBytes.value > 0) {
    rows.push({
      bytes: remainingBytes.value,
      dot: "bg-default ring-1 ring-inset ring-(--ui-border-accented)",
      key: "free",
      label: "Free",
      percent: formatShare(remainingBytes.value, base),
    });
  }

  return rows;
});

// Without a quota there is nothing to fill, so the ring stays an empty track; over quota the
// whole ring turns into the error color instead of pretending the segments still fit.
const arcs = computed<RingArc[]>(() => {
  const storage = data.value;
  if (!storage || isUnlimited.value) return [];

  if (remainingBytes.value < 0) {
    return [{ color: "var(--ui-error)", key: "over", length: 1, offset: 0 }];
  }

  return buildRingArcs(
    SEGMENTS.map((segment) => ({
      color: segment.color,
      key: segment.key,
      value: storage[segment.field],
    })),
    quotaBytes.value,
  );
});

const subtitle = computed(() => {
  const parts: string[] = [];

  if (authStore.isAdmin) parts.push("Admin");
  if (data.value && !isUnlimited.value) parts.push(`${Math.round(usagePercent.value)}% used`);

  return parts.join(" · ");
});

const go = (path: string) => {
  open.value = false;
  void router.push(path);
};

const reportProblem = () => {
  open.value = false;
  emit("reportProblem");
};

const logOut = () => {
  open.value = false;
  emit("logout");
};

const tiles = computed(() => [
  {
    active: route.name === "account",
    icon: "i-lucide-user-round",
    label: "Account",
    select: () => go("/account"),
  },
  {
    active: route.name === "settings",
    icon: "i-lucide-settings",
    label: "Settings",
    select: () => go("/settings"),
  },
  { active: false, icon: "i-lucide-life-buoy", label: "Report", select: reportProblem },
]);

const tileClass = (active: boolean) => {
  if (active) return "bg-primary/10 text-primary";

  return "text-gray-600 hover:bg-elevated hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100";
};

// Expanded, the card opens above the chip sharing its left edge; in the icon rail it opens beside
// the avatar with matching bottom edges.
const menuContent = computed(() => {
  if (collapsed) {
    return { align: "end" as const, collisionPadding: 8, side: "right" as const, sideOffset: 8 };
  }

  return { align: "start" as const, collisionPadding: 8, side: "top" as const, sideOffset: 6 };
});
</script>
