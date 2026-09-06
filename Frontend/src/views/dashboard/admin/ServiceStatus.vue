<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { HealthCheckEntry, HealthStatus } from "@/api/status";

import { serverResourceUsage, serverStatus } from "@/queries/status";

const { data, status, asyncStatus, refresh } = useQuery(serverStatus());
const { data: resources, refresh: refreshResources } = useQuery(serverResourceUsage());

const isLoading = computed(() => status.value === "pending");
const isError = computed(() => status.value === "error");
const isFetching = computed(() => asyncStatus.value === "loading");

const handleRefresh = () => {
  refresh();
  refreshResources();
};

const overall = computed(() => data.value?.status ?? null);

const statusConfig: Record<
  HealthStatus,
  {
    label: string;
    icon: string;
    ring: string;
    bg: string;
    text: string;
    dot: string;
  }
> = {
  Degraded: {
    bg: "bg-amber-500/10",
    dot: "bg-amber-500",
    icon: "mdi:alert-circle",
    label: "Degraded",
    ring: "ring-amber-400/60",
    text: "text-amber-600 dark:text-amber-400",
  },
  Healthy: {
    bg: "bg-emerald-500/10",
    dot: "bg-emerald-500",
    icon: "mdi:check-circle",
    label: "Healthy",
    ring: "ring-emerald-400/60",
    text: "text-emerald-600 dark:text-emerald-400",
  },
  Unhealthy: {
    bg: "bg-red-500/10",
    dot: "bg-red-500",
    icon: "mdi:close-circle",
    label: "Unhealthy",
    ring: "ring-red-400/60",
    text: "text-red-600 dark:text-red-400",
  },
};

const overallConfig = computed(() => (overall.value ? statusConfig[overall.value] : null));

const lastChecked = computed(() => {
  if (!data.value?.checkedAt) {
    return null;
  }
  return new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(new Date(data.value.checkedAt));
});

const totalDuration = computed(() => (data.value ? `${data.value.duration.toFixed(0)} ms` : null));

const sortedChecks = computed<HealthCheckEntry[]>(() => {
  if (!data.value?.checks) {
    return [];
  }
  const order: Record<HealthStatus, number> = {
    Degraded: 1,
    Healthy: 2,
    Unhealthy: 0,
  };
  return [...data.value.checks].sort((a, b) => order[a.status] - order[b.status]);
});

const formatMs = (ms: number) =>
  ms >= 1000 ? `${(ms / 1000).toFixed(2)} s` : `${ms.toFixed(0)} ms`;

// Explicit names/icons for the checks the server reports today. New checks
// that aren't listed here fall through to fallbackLabel below instead of
// breaking, but won't get a tailored icon or name until added here.
interface CheckDisplayMeta {
  label: string;
  icon: string;
}

const CHECK_DISPLAY_META: Record<string, CheckDisplayMeta> = {
  postgres: { icon: "mdi:database-outline", label: "Database" },
  rabbitmq: { icon: "mdi:swap-horizontal-circle-outline", label: "Message Queue" },
  storage: { icon: "mdi:folder-network-outline", label: "File Storage" },
  "worker-documentworker": { icon: "mdi:file-document-outline", label: "Document Worker" },
  "worker-lyricsworker": { icon: "mdi:script-text-outline", label: "Lyrics Worker" },
  "worker-mediametadataworker": { icon: "mdi:tag-text-outline", label: "Media Metadata Worker" },
  "worker-mediaworker": { icon: "mdi:file-music-outline", label: "Media Worker" },
  "worker-transpilationworker": { icon: "mdi:file-swap-outline", label: "Transpilation Worker" },
};

const fallbackLabel = (name: string): string =>
  name
    .replace(/^worker-/, "")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[_-]/g, " ")
    .replace(/\b\w/g, (char) => char.toUpperCase());

const checkLabel = (name: string): string => CHECK_DISPLAY_META[name]?.label ?? fallbackLabel(name);
const checkIcon = (name: string): string =>
  CHECK_DISPLAY_META[name]?.icon ?? "mdi:help-circle-outline";

const GROUP_META: Record<string, { label: string; icon: string }> = {
  infrastructure: { icon: "mdi:server-network", label: "Infrastructure" },
  worker: { icon: "mdi:cog-outline", label: "Workers" },
};

interface CheckGroup {
  key: string;
  label: string;
  icon: string;
  hasIssues: boolean;
  checks: HealthCheckEntry[];
}

// Groups preserve the severity ordering already applied by sortedChecks,
// so unhealthy/degraded checks still surface first within their group.
const groupedChecks = computed<CheckGroup[]>(() => {
  const groups = new Map<string, HealthCheckEntry[]>();
  for (const check of sortedChecks.value) {
    const key = check.tags[0] ?? "other";
    const bucket = groups.get(key);
    if (bucket) {
      bucket.push(check);
    } else {
      groups.set(key, [check]);
    }
  }
  return Array.from(groups.entries()).map(([key, checks]) => ({
    checks,
    hasIssues: checks.some((check) => check.status !== "Healthy"),
    icon: GROUP_META[key]?.icon ?? "mdi:shape-outline",
    key,
    label: GROUP_META[key]?.label ?? fallbackLabel(key),
  }));
});

const proc = computed(() => resources.value?.process ?? null);

const formatUptime = (raw: string): string => {
  let days = 0,
    hours = 0,
    minutes = 0;
  const dayMatch = raw.match(/^(\d+)\./);
  if (dayMatch) {
    days = parseInt(dayMatch[1]);
    raw = raw.slice(dayMatch[0].length);
  }
  const parts = raw.split(":").map(Number);
  hours = parts[0] ?? 0;
  minutes = parts[1] ?? 0;
  const pieces: string[] = [];
  if (days) {
    pieces.push(`${days}d`);
  }
  if (hours) {
    pieces.push(`${hours}h`);
  }
  if (minutes) {
    pieces.push(`${minutes}m`);
  }
  return pieces.length ? pieces.join(" ") : "just started";
};

const memoryColor = (pct: number) => {
  if (pct < 60) {
    return {
      bar: "bg-emerald-500",
      bg: "bg-emerald-500/10",
      ring: "ring-emerald-400/40",
      text: "text-emerald-600 dark:text-emerald-400",
    };
  }
  if (pct < 80) {
    return {
      bar: "bg-amber-500",
      bg: "bg-amber-500/10",
      ring: "ring-amber-400/40",
      text: "text-amber-600 dark:text-amber-400",
    };
  }
  return {
    bar: "bg-red-500",
    bg: "bg-red-500/10",
    ring: "ring-red-400/40",
    text: "text-red-600 dark:text-red-400",
  };
};

const memPct = computed(() => Math.min(proc.value?.memoryUsagePercent ?? 0, 100));
const memColor = computed(() => memoryColor(memPct.value));
const memLabel = computed(() => {
  if (memPct.value < 60) {
    return "Comfortable";
  }
  if (memPct.value < 80) {
    return "Getting full";
  }
  return "Nearly full";
});

const formatMb = (mb: number) =>
  mb >= 1024 ? `${(mb / 1024).toFixed(1)} GB` : `${mb.toFixed(0)} MB`;

const formatCpuTime = (s: number) => {
  if (s < 60) {
    return `${s.toFixed(1)}s`;
  }
  if (s < 3600) {
    return `${Math.floor(s / 60)}m ${Math.round(s % 60)}s`;
  }
  return `${Math.floor(s / 3600)}h ${Math.floor((s % 3600) / 60)}m`;
};
</script>

<template>
  <div class="p-3 sm:p-5 lg:p-6">
    <!-- Page header -->
    <div class="flex items-center justify-between mb-4 max-w-400 mx-auto">
      <div>
        <h1 class="text-lg font-semibold text-gray-800 dark:text-gray-100">System Health</h1>
        <p v-if="lastChecked" class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
          Last checked at {{ lastChecked }}
          <span v-if="totalDuration" class="ml-1 opacity-60">· {{ totalDuration }} total</span>
        </p>
      </div>
      <div class="flex items-center gap-4">
        <UButton
          size="sm"
          color="neutral"
          variant="outline"
          :loading="isFetching"
          icon="i-mdi-refresh"
          @click="handleRefresh()"
        >
          Refresh
        </UButton>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="isLoading" class="flex items-center justify-center py-32">
      <UIcon name="i-mdi-loading" class="w-7 h-7 text-gray-400 animate-spin" />
    </div>

    <!-- Error -->
    <UAlert
      v-else-if="isError"
      color="error"
      variant="soft"
      title="Could not reach server"
      description="Unable to fetch health status. The server may be offline."
      icon="i-mdi-connection"
      class="max-w-400 mx-auto"
    />

    <template v-else-if="data">
      <div class="max-w-400 mx-auto space-y-3 lg:space-y-4">
        <!-- Row 1: Health overview -->
        <div
          class="grid grid-cols-1 lg:grid-cols-[300px_1fr] xl:grid-cols-[340px_1fr] gap-3 lg:gap-4 items-start"
        >
          <!-- Left: status summary -->
          <div class="flex flex-col gap-3">
            <!-- Overall status card -->
            <div
              class="rounded-2xl border frosted-glass px-5 py-4 ring-1 border-transparent transition-all duration-200"
              :class="[overallConfig?.bg, overallConfig?.ring]"
            >
              <p
                class="text-xs font-medium uppercase tracking-widest text-gray-500 dark:text-gray-400 mb-3"
              >
                Overall Status
              </p>
              <div class="flex items-center gap-3">
                <Icon
                  :icon="overallConfig?.icon ?? 'mdi:help-circle'"
                  class="w-11 h-11 shrink-0"
                  :class="overallConfig?.text"
                />
                <p class="text-2xl font-bold leading-none" :class="overallConfig?.text">
                  {{ overallConfig?.label }}
                </p>
              </div>
            </div>

            <!-- Summary counts -->
            <div class="grid grid-cols-3 gap-2">
              <div
                class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface py-3 px-2 flex flex-col items-center gap-1"
              >
                <span class="w-2 h-2 rounded-full bg-emerald-500 mb-0.5" />
                <span class="text-xl font-bold text-gray-800 dark:text-gray-100">{{
                  data.summary.healthy
                }}</span>
                <span class="text-xs text-gray-500 dark:text-gray-400">Healthy</span>
              </div>
              <div
                class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface py-3 px-2 flex flex-col items-center gap-1"
              >
                <span class="w-2 h-2 rounded-full bg-amber-500 mb-0.5" />
                <span class="text-xl font-bold text-gray-800 dark:text-gray-100">{{
                  data.summary.degraded
                }}</span>
                <span class="text-xs text-gray-500 dark:text-gray-400">Degraded</span>
              </div>
              <div
                class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface py-3 px-2 flex flex-col items-center gap-1"
              >
                <span class="w-2 h-2 rounded-full bg-red-500 mb-0.5" />
                <span class="text-xl font-bold text-gray-800 dark:text-gray-100">{{
                  data.summary.unhealthy
                }}</span>
                <span class="text-xs text-gray-500 dark:text-gray-400">Unhealthy</span>
              </div>
            </div>

            <!-- Meta info -->
            <div
              class="flex flex-col rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface divide-y divide-gray-200/70 dark:divide-gray-700/70"
            >
              <div class="px-4 py-2.5 flex items-center justify-between">
                <span class="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-2">
                  <Icon icon="mdi:clock-outline" class="w-4 h-4" />Checked at
                </span>
                <span class="text-xs font-medium text-gray-700 dark:text-gray-300">{{
                  lastChecked ?? "—"
                }}</span>
              </div>
              <div class="px-4 py-2.5 flex items-center justify-between">
                <span class="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-2">
                  <Icon icon="mdi:timer-outline" class="w-4 h-4" />Total duration
                </span>
                <span class="text-xs font-medium text-gray-700 dark:text-gray-300">{{
                  totalDuration ?? "—"
                }}</span>
              </div>
              <div class="px-4 py-2.5 flex items-center justify-between">
                <span class="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-2">
                  <Icon icon="mdi:format-list-checks" class="w-4 h-4" />Total checks
                </span>
                <span class="text-xs font-medium text-gray-700 dark:text-gray-300">{{
                  data.checks.length
                }}</span>
              </div>
            </div>
          </div>

          <!-- Right: grouped checks -->
          <div class="flex flex-col gap-3">
            <div
              v-for="group in groupedChecks"
              :key="group.key"
              class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden"
            >
              <div
                class="px-4 py-2.5 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2"
              >
                <Icon :icon="group.icon" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
                <span class="text-sm font-semibold text-gray-700 dark:text-gray-300">{{
                  group.label
                }}</span>
                <span
                  class="ml-auto text-xs font-medium"
                  :class="
                    group.hasIssues
                      ? 'text-red-600 dark:text-red-400'
                      : 'text-gray-500 dark:text-gray-500'
                  "
                >
                  {{ group.hasIssues ? "Needs attention" : `${group.checks.length} healthy` }}
                </span>
              </div>

              <div
                v-for="(check, index) in group.checks"
                :key="check.name"
                class="px-4 py-3 grid grid-cols-[14px_1fr_80px_100px] gap-4 items-start transition-colors hover:bg-black/[0.02] dark:hover:bg-white/[0.02]"
                :class="{ 'border-t border-gray-200/70 dark:border-gray-700/70': index > 0 }"
              >
                <span
                  class="w-2.5 h-2.5 rounded-full mt-1 shrink-0"
                  :class="statusConfig[check.status].dot"
                />
                <div class="min-w-0">
                  <div class="flex items-center gap-1.5">
                    <Icon
                      :icon="checkIcon(check.name)"
                      class="w-3.5 h-3.5 text-gray-400 dark:text-gray-500 shrink-0"
                    />
                    <p class="text-sm font-medium text-gray-800 dark:text-gray-100">
                      {{ checkLabel(check.name) }}
                    </p>
                  </div>
                  <p
                    v-if="check.error || check.description"
                    class="text-xs mt-0.5 truncate"
                    :class="
                      check.error
                        ? 'text-red-600 dark:text-red-400'
                        : 'text-gray-500 dark:text-gray-400'
                    "
                  >
                    {{ check.error ?? check.description }}
                  </p>
                </div>
                <span
                  class="text-xs text-gray-500 dark:text-gray-400 text-right tabular-nums pt-0.5"
                >
                  {{ formatMs(check.duration) }}
                </span>
                <div class="flex justify-end pt-0.5">
                  <span
                    class="text-xs font-semibold px-2.5 py-1 rounded-full"
                    :class="[statusConfig[check.status].bg, statusConfig[check.status].text]"
                    >{{ statusConfig[check.status].label }}</span
                  >
                </div>
              </div>
            </div>

            <div
              v-if="!groupedChecks.length"
              class="rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 px-5 py-12 text-center text-sm text-gray-400 dark:text-gray-500"
            >
              No checks reported.
            </div>
          </div>
        </div>

        <div
          v-if="proc"
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden"
        >
          <!-- Section header -->
          <div
            class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2"
          >
            <Icon icon="mdi:server" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
            <span class="text-sm font-semibold text-gray-700 dark:text-gray-300"
              >Resource Usage</span
            >
            <span class="ml-auto text-xs text-gray-500 dark:text-gray-400">Live process stats</span>
          </div>

          <div
            class="grid grid-cols-1 lg:grid-cols-[2fr_1.6fr_1fr] divide-y lg:divide-y-0 lg:divide-x divide-gray-200/60 dark:divide-gray-700/60"
          >
            <div class="p-4">
              <div
                class="rounded-xl border p-4 h-full flex flex-col gap-3 justify-between ring-1 border-transparent"
                :class="[memColor.bg, memColor.ring]"
              >
                <!-- Header row -->
                <div class="flex items-start justify-between gap-4">
                  <div class="flex items-center gap-2">
                    <Icon icon="mdi:memory" class="w-4 h-4" :class="memColor.text" />
                    <p class="text-sm font-semibold text-gray-800 dark:text-gray-100">
                      Memory Usage
                    </p>
                    <UTooltip :delay-duration="200" :ui="{ content: 'tooltip-content' }">
                      <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      <template #content>
                        <div class="h-full max-w-52 p-1 space-y-1">
                          <p class="font-semibold text-xs">Memory Usage</p>
                          <p class="text-xs text-gray-400">
                            How much of the server's available RAM is in use — like the memory on
                            your own computer. When it's nearly full, things slow down or crash.
                          </p>
                          <p class="text-xs text-emerald-400 font-medium">Lower is better.</p>
                        </div>
                      </template>
                    </UTooltip>
                  </div>
                  <div class="text-right shrink-0">
                    <p class="text-2xl font-bold tabular-nums" :class="memColor.text">
                      {{ memPct.toFixed(0) }}%
                    </p>
                    <p class="text-xs font-medium mt-0.5" :class="memColor.text">
                      {{ memLabel }}
                    </p>
                  </div>
                </div>
                <!-- Progress bar -->
                <div class="h-2 rounded-full bg-black/10 dark:bg-white/10 overflow-hidden">
                  <div
                    class="h-full rounded-full transition-all duration-500"
                    :class="memColor.bar"
                    :style="{ width: `${memPct}%` }"
                  />
                </div>
                <!-- Sub-stats -->
                <div class="grid grid-cols-3 gap-3">
                  <div class="text-center">
                    <p
                      class="text-xs text-gray-500 dark:text-gray-400 flex items-center justify-center gap-1"
                    >
                      Active use
                      <UTooltip
                        text="Memory actively being read or written right now."
                        :delay-duration="200"
                      >
                        <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      </UTooltip>
                    </p>
                    <p class="text-sm font-semibold text-gray-700 dark:text-gray-200 tabular-nums">
                      {{ formatMb(proc.workingSetMb) }}
                    </p>
                  </div>
                  <div class="text-center border-x border-gray-200/60 dark:border-gray-700/60">
                    <p
                      class="text-xs text-gray-500 dark:text-gray-400 flex items-center justify-center gap-1"
                    >
                      Allocated
                      <UTooltip
                        text="Total memory reserved by the server, including cached data it may release if needed."
                        :delay-duration="200"
                      >
                        <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      </UTooltip>
                    </p>
                    <p class="text-sm font-semibold text-gray-700 dark:text-gray-200 tabular-nums">
                      {{ formatMb(proc.gcTotalMemoryMb) }}
                    </p>
                  </div>
                  <div class="text-center">
                    <p
                      class="text-xs text-gray-500 dark:text-gray-400 flex items-center justify-center gap-1"
                    >
                      Limit
                      <UTooltip
                        text="The maximum memory the server is allowed to use. Going over this causes it to crash."
                        :delay-duration="200"
                      >
                        <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      </UTooltip>
                    </p>
                    <p class="text-sm font-semibold text-gray-700 dark:text-gray-200 tabular-nums">
                      {{ formatMb(proc.memoryLimitMb) }}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div class="p-4 flex flex-col gap-3">
              <!-- Sub-header -->
              <div class="flex items-center gap-1.5">
                <Icon icon="mdi:recycle" class="w-3.5 h-3.5 text-gray-400 dark:text-gray-500" />
                <span
                  class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider"
                >
                  Memory Cleanups
                </span>
                <UTooltip :delay-duration="200" :ui="{ content: 'tooltip-content' }">
                  <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                  <template #content>
                    <div class="max-w-64 p-1 space-y-1">
                      <p class="font-semibold text-xs">Memory Cleanups (Garbage Collection)</p>
                      <p class="text-xs text-gray-400">
                        The server periodically reclaims memory it no longer needs. Three levels —
                        Routine is harmless, Deep is more intensive.
                      </p>
                      <p class="text-xs text-gray-400">
                        Frequent
                        <span class="text-red-500 font-medium">Deep</span>
                        cleanups may indicate memory pressure.
                      </p>
                    </div>
                  </template>
                </UTooltip>
              </div>

              <!-- GC rows -->
              <div
                class="flex flex-col divide-y divide-gray-200/60 dark:divide-gray-700/60 rounded-xl border border-gray-200/70 dark:border-gray-700/70 overflow-hidden flex-1"
              >
                <div
                  class="flex items-center justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-emerald-500 shrink-0" />
                    <div>
                      <p class="text-xs font-medium text-gray-700 dark:text-gray-300">Routine</p>
                      <p class="text-[11px] text-gray-500 dark:text-gray-400 leading-tight">
                        Quick sweeps, always expected
                      </p>
                    </div>
                  </div>
                  <span class="text-lg font-bold text-gray-800 dark:text-gray-100 tabular-nums">
                    {{ proc.gen0Collections }}
                  </span>
                </div>
                <div
                  class="flex items-center justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-amber-500 shrink-0" />
                    <div>
                      <p class="text-xs font-medium text-gray-700 dark:text-gray-300">Moderate</p>
                      <p class="text-[11px] text-gray-500 dark:text-gray-400 leading-tight">
                        Deeper, less frequent, normal
                      </p>
                    </div>
                  </div>
                  <span class="text-lg font-bold text-gray-800 dark:text-gray-100 tabular-nums">
                    {{ proc.gen1Collections }}
                  </span>
                </div>
                <div
                  class="flex items-center justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-red-500 shrink-0" />
                    <div>
                      <p class="text-xs font-medium text-gray-700 dark:text-gray-300">Deep</p>
                      <p class="text-[11px] text-gray-500 dark:text-gray-400 leading-tight">
                        Full clean — frequent may mean pressure
                      </p>
                    </div>
                  </div>
                  <span class="text-lg font-bold text-gray-800 dark:text-gray-100 tabular-nums">
                    {{ proc.gen2Collections }}
                  </span>
                </div>
              </div>
            </div>

            <div class="p-4 flex flex-col gap-3">
              <!-- Sub-header -->
              <div class="flex items-center gap-1.5">
                <Icon icon="mdi:cpu-64-bit" class="w-3.5 h-3.5 text-gray-400 dark:text-gray-500" />
                <span
                  class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider"
                  >Process</span
                >
              </div>

              <!-- Stat rows -->
              <div
                class="flex flex-col divide-y divide-gray-200/60 dark:divide-gray-700/60 rounded-xl border border-gray-200/70 dark:border-gray-700/70 overflow-hidden flex-1"
              >
                <!-- Uptime -->
                <div
                  class="flex items-center flex-1 justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-1.5">
                    <Icon icon="mdi:timer-sand" class="w-3.5 h-3.5 text-primary shrink-0" />
                    <span class="text-xs text-gray-500 dark:text-gray-400">Uptime</span>
                    <UTooltip :delay-duration="200" :ui="{ content: 'tooltip-content' }">
                      <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      <template #content>
                        <div class="max-w-52 p-1 space-y-1">
                          <p class="font-semibold text-xs">Uptime</p>
                          <p class="text-xs text-gray-400">
                            How long the server has been running without a restart.
                          </p>
                          <p class="text-xs text-emerald-400 font-medium">Longer is better.</p>
                        </div>
                      </template>
                    </UTooltip>
                  </div>
                  <p class="text-sm font-bold text-gray-800 dark:text-gray-100 tabular-nums">
                    {{ formatUptime(proc.uptime) }}
                  </p>
                </div>
                <!-- CPU Time -->
                <div
                  class="flex items-center flex-1 justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-1.5">
                    <Icon icon="mdi:cpu-64-bit" class="w-3.5 h-3.5 text-primary shrink-0" />
                    <span class="text-xs text-gray-500 dark:text-gray-400">CPU Time</span>
                    <UTooltip :delay-duration="200" :ui="{ content: 'tooltip-content' }">
                      <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      <template #content>
                        <div class="max-w-52 p-1 space-y-1">
                          <p class="font-semibold text-xs">CPU Time</p>
                          <p class="text-xs text-gray-400">
                            Total computing work done since startup. Grows naturally.
                          </p>
                        </div>
                      </template>
                    </UTooltip>
                  </div>
                  <p class="text-sm font-bold text-gray-800 dark:text-gray-100 tabular-nums">
                    {{ formatCpuTime(proc.cpuTimeSeconds) }}
                  </p>
                </div>
                <!-- Threads -->
                <div
                  class="flex items-center flex-1 justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-1.5">
                    <Icon icon="mdi:source-branch" class="w-3.5 h-3.5 text-primary shrink-0" />
                    <span class="text-xs text-gray-500 dark:text-gray-400">Threads</span>
                    <UTooltip :delay-duration="200" :ui="{ content: 'tooltip-content' }">
                      <UIcon name="i-lucide-info" class="size-3 text-muted/60 cursor-help" />
                      <template #content>
                        <div class="max-w-52 p-1 space-y-1">
                          <p class="font-semibold text-xs">Active Threads</p>
                          <p class="text-xs text-gray-400">
                            Tasks the server handles in parallel. Steady is healthy.
                          </p>
                        </div>
                      </template>
                    </UTooltip>
                  </div>
                  <p class="text-sm font-bold text-gray-800 dark:text-gray-100 tabular-nums">
                    {{ proc.threadCount }}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
