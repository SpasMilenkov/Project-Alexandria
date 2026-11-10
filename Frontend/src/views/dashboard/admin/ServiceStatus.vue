<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { HealthCheckEntry, HealthStatus } from "@/api/status";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import InfoHint from "@/components/common/InfoHint.vue";
import { serverResourceUsage, serverStatus } from "@/queries/status";
import { formatClockTime, formatDuration } from "@/utils/date-formatters";

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

const OVERALL_BADGE_COLORS: Record<HealthStatus, "success" | "warning" | "error"> = {
  Degraded: "warning",
  Healthy: "success",
  Unhealthy: "error",
};

const overallBadgeColor = computed(() =>
  overall.value ? OVERALL_BADGE_COLORS[overall.value] : "neutral",
);

const lastChecked = computed(() => {
  if (!data.value?.checkedAt) {
    return null;
  }
  return formatClockTime(data.value.checkedAt, { seconds: true });
});

const totalDuration = computed(() => (data.value ? formatDuration(data.value.duration / 1000) : null));

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

// The API sends a .NET TimeSpan, "d.hh:mm:ss.fffffff", with the day part only past 24 hours.
const TIME_SPAN = /^(?:(?<days>\d+)\.)?(?<hours>\d+):(?<minutes>\d+):(?<seconds>\d+(?:\.\d+)?)$/u;

const formatProcessUptime = (raw: string): string => {
  const parts = TIME_SPAN.exec(raw)?.groups;
  if (!parts) return raw;

  const seconds =
    Number(parts.days ?? 0) * 86_400 +
    Number(parts.hours) * 3600 +
    Number(parts.minutes) * 60 +
    Number(parts.seconds);
  if (seconds < 60) return "just started";

  // Seconds are dropped so a long uptime reads "3h 4m", not "3h 4m 5s".
  return formatDuration(Math.floor(seconds / 60) * 60, { maxUnits: 3 });
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

const MEMORY_LEVELS = [
  { label: "Comfortable", range: "Under 60%", bar: "bg-emerald-500" },
  { label: "Getting full", range: "60 to 80%", bar: "bg-amber-500" },
  { label: "Nearly full", range: "80% and up", bar: "bg-red-500" },
];

// Uptime arrives as a .NET TimeSpan string: "[d.]hh:mm:ss[.fffffff]".
const uptimeSeconds = (raw: string): number | null => {
  const match =
    /^(?:(?<days>\d+)\.)?(?<hours>\d+):(?<minutes>\d+):(?<seconds>\d+(?:\.\d+)?)$/u.exec(raw);
  const parts = match?.groups;

  if (!parts) return null;

  return (
    Number(parts.days ?? 0) * 86_400 +
    Number(parts.hours) * 3600 +
    Number(parts.minutes) * 60 +
    Number(parts.seconds)
  );
};

const cpuAverage = computed(() => {
  const stats = proc.value;

  if (!stats) return null;

  const elapsed = uptimeSeconds(stats.uptime);

  if (!elapsed || elapsed < 60) return null;

  const share = (stats.cpuTimeSeconds / elapsed) * 100;

  return `${share < 10 ? share.toFixed(1) : share.toFixed(0)}%`;
});

const formatMb = (mb: number) =>
  mb >= 1024 ? `${(mb / 1024).toFixed(1)} GB` : `${mb.toFixed(0)} MB`;
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-activity" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          System vitals
        </h1>
        <UBadge v-if="overallConfig" :color="overallBadgeColor" variant="subtle" size="sm">
          {{ overallConfig.label }}
        </UBadge>
      </div>

      <div class="flex shrink-0 items-center gap-2">
        <span
          v-if="lastChecked"
          class="hidden text-xs text-gray-600 tabular-nums sm:inline dark:text-gray-400"
        >
          Checked {{ lastChecked }}
        </span>

        <UTooltip text="Refresh">
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-refresh-cw"
            square
            class="justify-center"
            aria-label="Refresh system vitals"
            :loading="isFetching"
            @click="handleRefresh()"
          />
        </UTooltip>
      </div>
    </header>

    <!-- Loading -->
    <div v-if="isLoading" class="flex items-center justify-center py-32">
      <BlockSpinner :size="24" aria-label="Loading system vitals" class="text-gray-500" />
    </div>

    <!-- Error -->
    <UAlert
      v-else-if="isError"
      color="error"
      variant="soft"
      title="Could not reach server"
      description="Unable to fetch health status. The server may be offline."
      icon="i-mdi-connection"
    />

    <template v-else-if="data">
      <div class="space-y-3 lg:space-y-4">
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
                class="text-xs font-medium uppercase tracking-widest text-gray-600 dark:text-gray-400 mb-3"
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
                <span class="text-xl font-bold text-gray-900 dark:text-gray-100">{{
                  data.summary.healthy
                }}</span>
                <span class="text-xs text-gray-600 dark:text-gray-400">Healthy</span>
              </div>
              <div
                class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface py-3 px-2 flex flex-col items-center gap-1"
              >
                <span class="w-2 h-2 rounded-full bg-amber-500 mb-0.5" />
                <span class="text-xl font-bold text-gray-900 dark:text-gray-100">{{
                  data.summary.degraded
                }}</span>
                <span class="text-xs text-gray-600 dark:text-gray-400">Degraded</span>
              </div>
              <div
                class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface py-3 px-2 flex flex-col items-center gap-1"
              >
                <span class="w-2 h-2 rounded-full bg-red-500 mb-0.5" />
                <span class="text-xl font-bold text-gray-900 dark:text-gray-100">{{
                  data.summary.unhealthy
                }}</span>
                <span class="text-xs text-gray-600 dark:text-gray-400">Unhealthy</span>
              </div>
            </div>

            <!-- Meta info -->
            <div
              class="flex flex-col rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface divide-y divide-gray-200/70 dark:divide-gray-700/70"
            >
              <div class="px-4 py-2.5 flex items-center justify-between">
                <span class="text-xs text-gray-600 dark:text-gray-400 flex items-center gap-2">
                  <Icon icon="mdi:clock-outline" class="w-4 h-4" />Checked at
                </span>
                <span class="text-xs font-medium text-gray-900 dark:text-gray-100">{{
                  lastChecked ?? "—"
                }}</span>
              </div>
              <div class="px-4 py-2.5 flex items-center justify-between">
                <span class="text-xs text-gray-600 dark:text-gray-400 flex items-center gap-2">
                  <Icon icon="mdi:timer-outline" class="w-4 h-4" />Total duration
                </span>
                <span class="text-xs font-medium text-gray-900 dark:text-gray-100">{{
                  totalDuration ?? "—"
                }}</span>
              </div>
              <div class="px-4 py-2.5 flex items-center justify-between">
                <span class="text-xs text-gray-600 dark:text-gray-400 flex items-center gap-2">
                  <Icon icon="mdi:format-list-checks" class="w-4 h-4" />Total checks
                </span>
                <span class="text-xs font-medium text-gray-900 dark:text-gray-100">{{
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
                <Icon :icon="group.icon" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
                <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">{{
                  group.label
                }}</span>
                <span
                  class="ml-auto text-xs font-medium"
                  :class="
                    group.hasIssues
                      ? 'text-red-600 dark:text-red-400'
                      : 'text-gray-600 dark:text-gray-400'
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
                      class="w-3.5 h-3.5 text-gray-600 dark:text-gray-400 shrink-0"
                    />
                    <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                      {{ checkLabel(check.name) }}
                    </p>
                  </div>
                  <p
                    v-if="check.error || check.description"
                    class="text-xs mt-0.5 truncate"
                    :class="
                      check.error
                        ? 'text-red-600 dark:text-red-400'
                        : 'text-gray-600 dark:text-gray-400'
                    "
                  >
                    {{ check.error ?? check.description }}
                  </p>
                </div>
                <span
                  class="text-xs text-gray-600 dark:text-gray-400 text-right tabular-nums pt-0.5"
                >
                  {{ formatDuration(check.duration / 1000) }}
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
              class="rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70 px-5 py-12 text-center text-sm text-gray-600 dark:text-gray-400"
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
            <Icon icon="mdi:server" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
            <span class="text-sm font-semibold text-gray-900 dark:text-gray-100"
              >Resource Usage</span
            >
            <span class="ml-auto text-xs text-gray-600 dark:text-gray-400">Live process stats</span>
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
                    <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">
                      Memory Usage
                    </p>
                    <InfoHint
                      title="Memory usage"
                      icon="i-mdi-memory"
                      guidance="Lower is better. Staying near the limit for long is worth investigating."
                    >
                      How much of the server's memory limit is in use right now. When it fills up,
                      requests slow down and the server can be stopped.

                      <template #detail>
                        <ol
                          class="grid grid-cols-3 gap-1 rounded-xl bg-gray-500/10 p-1.5"
                          aria-label="Memory levels"
                        >
                          <li
                            v-for="level in MEMORY_LEVELS"
                            :key="level.label"
                            :aria-current="level.label === memLabel ? 'true' : undefined"
                            class="flex flex-col gap-1.5 rounded-lg px-2 py-2"
                            :class="{
                              'bg-white shadow-sm dark:bg-neutral-800': level.label === memLabel,
                            }"
                          >
                            <span class="h-1.5 w-full rounded-full" :class="level.bar" />
                            <span class="text-xs font-semibold text-gray-900 dark:text-gray-100">{{
                              level.label
                            }}</span>
                            <span class="text-xs text-gray-600 dark:text-gray-400">{{
                              level.range
                            }}</span>
                          </li>
                        </ol>
                      </template>
                    </InfoHint>
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
                      class="text-xs text-gray-600 dark:text-gray-400 flex items-center justify-center gap-1"
                    >
                      Active use
                      <InfoHint title="Active use" icon="i-mdi-memory">
                        Physical memory the server process occupies right now, including its code
                        and cached data. This is the number measured against the limit.
                      </InfoHint>
                    </p>
                    <p class="text-sm font-semibold text-gray-900 dark:text-gray-100 tabular-nums">
                      {{ formatMb(proc.workingSetMb) }}
                    </p>
                  </div>
                  <div class="text-center border-x border-gray-200/60 dark:border-gray-700/60">
                    <p
                      class="text-xs text-gray-600 dark:text-gray-400 flex items-center justify-center gap-1"
                    >
                      Allocated
                      <InfoHint title="Allocated" icon="i-mdi-memory">
                        Memory held by the server's own data right now. Cleanups hand part of it
                        back, so this number rises and falls on its own.
                      </InfoHint>
                    </p>
                    <p class="text-sm font-semibold text-gray-900 dark:text-gray-100 tabular-nums">
                      {{ formatMb(proc.gcTotalMemoryMb) }}
                    </p>
                  </div>
                  <div class="text-center">
                    <p
                      class="text-xs text-gray-600 dark:text-gray-400 flex items-center justify-center gap-1"
                    >
                      Limit
                      <InfoHint title="Limit" icon="i-mdi-memory">
                        The most memory the server may use, set by its container or host. Going past
                        it gets the process stopped.
                      </InfoHint>
                    </p>
                    <p class="text-sm font-semibold text-gray-900 dark:text-gray-100 tabular-nums">
                      {{ formatMb(proc.memoryLimitMb) }}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div class="p-4 flex flex-col gap-3">
              <!-- Sub-header -->
              <div class="flex items-center gap-1.5">
                <Icon icon="mdi:recycle" class="w-3.5 h-3.5 text-gray-600 dark:text-gray-400" />
                <span
                  class="text-xs font-semibold text-gray-600 dark:text-gray-400 uppercase tracking-wider"
                >
                  Memory Cleanups
                </span>
                <InfoHint
                  title="Memory cleanups"
                  icon="i-mdi-recycle"
                  guidance="Routine and moderate cleanups are normal. Deep cleanups piling up quickly point to memory pressure."
                >
                  The runtime regularly frees memory the server no longer needs. These counts run
                  since the last restart.
                </InfoHint>
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
                      <p class="text-xs font-medium text-gray-900 dark:text-gray-100">Routine</p>
                      <p class="text-[11px] text-gray-600 dark:text-gray-400 leading-tight">
                        Quick sweeps, always expected
                      </p>
                    </div>
                  </div>
                  <span class="text-lg font-bold text-gray-900 dark:text-gray-100 tabular-nums">
                    {{ proc.gen0Collections }}
                  </span>
                </div>
                <div
                  class="flex items-center justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-amber-500 shrink-0" />
                    <div>
                      <p class="text-xs font-medium text-gray-900 dark:text-gray-100">Moderate</p>
                      <p class="text-[11px] text-gray-600 dark:text-gray-400 leading-tight">
                        Deeper, less frequent, normal
                      </p>
                    </div>
                  </div>
                  <span class="text-lg font-bold text-gray-900 dark:text-gray-100 tabular-nums">
                    {{ proc.gen1Collections }}
                  </span>
                </div>
                <div
                  class="flex items-center justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-red-500 shrink-0" />
                    <div>
                      <p class="text-xs font-medium text-gray-900 dark:text-gray-100">Deep</p>
                      <p class="text-[11px] text-gray-600 dark:text-gray-400 leading-tight">
                        Full clean — frequent may mean pressure
                      </p>
                    </div>
                  </div>
                  <span class="text-lg font-bold text-gray-900 dark:text-gray-100 tabular-nums">
                    {{ proc.gen2Collections }}
                  </span>
                </div>
              </div>
            </div>

            <div class="p-4 flex flex-col gap-3">
              <!-- Sub-header -->
              <div class="flex items-center gap-1.5">
                <Icon icon="mdi:cpu-64-bit" class="w-3.5 h-3.5 text-gray-600 dark:text-gray-400" />
                <span
                  class="text-xs font-semibold text-gray-600 dark:text-gray-400 uppercase tracking-wider"
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
                    <span class="text-xs text-gray-600 dark:text-gray-400">Uptime</span>
                    <InfoHint
                      title="Uptime"
                      icon="i-mdi-timer-sand"
                      guidance="A reset you didn't expect means the server restarted, possibly after a crash."
                    >
                      How long the server has been running since it last started. Restarts and
                      updates reset it.
                    </InfoHint>
                  </div>
                  <p class="text-sm font-bold text-gray-900 dark:text-gray-100 tabular-nums">
                    {{ formatProcessUptime(proc.uptime) }}
                  </p>
                </div>
                <!-- CPU Time -->
                <div
                  class="flex items-center flex-1 justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-1.5">
                    <Icon icon="mdi:cpu-64-bit" class="w-3.5 h-3.5 text-primary shrink-0" />
                    <span class="text-xs text-gray-600 dark:text-gray-400">CPU Time</span>
                    <InfoHint title="CPU time" icon="i-mdi-cpu-64-bit">
                      Processor time the server has used since it started. It only ever grows, so
                      read it next to uptime rather than on its own.

                      <template v-if="cpuAverage" #detail>
                        <p
                          class="rounded-xl bg-gray-500/10 px-3 py-2 text-sm text-gray-600 dark:text-gray-400"
                        >
                          <span
                            class="font-semibold text-gray-900 tabular-nums dark:text-gray-100"
                            >{{ cpuAverage }}</span
                          >
                          of one core on average since startup.
                        </p>
                      </template>
                    </InfoHint>
                  </div>
                  <p class="text-sm font-bold text-gray-900 dark:text-gray-100 tabular-nums">
                    {{ formatDuration(proc.cpuTimeSeconds) }}
                  </p>
                </div>
                <!-- Threads -->
                <div
                  class="flex items-center flex-1 justify-between px-4 py-3 bg-white/40 dark:bg-white/2"
                >
                  <div class="flex items-center gap-1.5">
                    <Icon icon="mdi:source-branch" class="w-3.5 h-3.5 text-primary shrink-0" />
                    <span class="text-xs text-gray-600 dark:text-gray-400">Threads</span>
                    <InfoHint
                      title="Threads"
                      icon="i-mdi-source-branch"
                      guidance="A steady count is healthy. One that keeps climbing can mean work is stuck waiting."
                    >
                      Work the server runs in parallel, from incoming requests to background jobs.
                    </InfoHint>
                  </div>
                  <p class="text-sm font-bold text-gray-900 dark:text-gray-100 tabular-nums">
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
