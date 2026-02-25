<template>
  <div class="flex flex-col min-h-screen px-6 py-10 font-garamond">
    <!-- Header -->
    <header class="max-w-7xl mx-auto w-full text-center mb-8">
      <h1
        class="font-playfair text-5xl font-bold tracking-tight leading-none mb-2"
      >
        Administration Console
      </h1>
      <p class="text-base italic">
        System management &amp; operational oversight
      </p>

      <div class="flex items-center justify-center gap-3 mt-4">
        <UBadge
          variant="outline"
          size="sm"
          :color="statusColor"
          class="rounded-full gap-1.5 opacity-70"
        >
          <span
            class="w-1.5 h-1.5 rounded-full"
            :class="`bg-${statusColor}-500`"
          />
          {{ systemStatus }}
        </UBadge>
        <time class="text-xs italic opacity-90">{{ currentDate }}</time>
      </div>
    </header>

    <!-- Navigation Grid -->
    <main
      class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 max-w-7xl mx-auto w-full"
    >
      <UCard
        v-for="section in sections"
        :key="section.id"
        as="RouterLink"
        @click="router.push(section.route)"
        :ui="{
          root: 'nav-card group relative overflow-hidden rounded-sm border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm hover:bg-white/75 dark:hover:bg-white/10 transition-all duration-200 no-underline text-inherit cursor-pointer',
          body: 'flex flex-col gap-4 p-5',
        }"
      >
        <!-- Top shelf line -->
        <span
          class="absolute top-0 left-0 right-0 h-0.5 bg-black/10 dark:bg-white/10 group-hover:bg-black/20 dark:group-hover:bg-white/20 transition-colors"
        />

        <!-- Icon + badges -->
        <div class="flex items-start justify-between gap-2 mt-1">
          <div
            class="p-2 rounded-sm border border-dashed border-black/15 dark:border-white/15 opacity-70"
          >
            <UIcon :name="section.icon" class="w-4 h-4" />
          </div>
          <div class="flex flex-wrap gap-1 justify-end">
            <UBadge
              v-for="badge in section.badges"
              :key="badge.label"
              :color="badgeColor(badge.type)"
              :variant="badge.type === 'neutral' ? 'outline' : 'subtle'"
              size="sm"
              class="rounded-sm text-[0.58rem] tracking-wider"
            >
              {{ badge.label }}
            </UBadge>
          </div>
        </div>

        <!-- Body -->
        <div class="flex-1">
          <p class="text-[0.6rem] uppercase tracking-[0.15em] opacity-80 mb-1">
            {{ section.chapter }}
          </p>
          <h2 class="font-playfair text-xl font-semibold leading-snug mb-2">
            {{ section.title }}
          </h2>
          <p class="text-sm leading-relaxed opacity-95">
            {{ section.description }}
          </p>
        </div>

        <!-- Footer -->
        <USeparator :ui="{ root: 'border-dashed opacity-40' }" />
        <div class="flex items-center gap-1">
          <span
            v-for="(tag, i) in section.tags"
            :key="tag"
            class="text-[0.62rem] italic opacity-85"
            >{{ tag }}{{ i < section.tags.length - 1 ? " ·" : "" }}</span
          >
          <UIcon
            name="mdi:arrow-right-thin"
            class="w-4 h-4 ml-auto opacity-30 group-hover:opacity-60 group-hover:translate-x-0.5 transition-all"
          />
        </div>
      </UCard>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

const currentDate = computed(() =>
  new Date().toLocaleDateString("en-GB", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  }),
);

const systemStatus = ref<"All Systems Nominal" | "Degraded" | "Outage">(
  "All Systems Nominal",
);

const statusColor = computed(() => {
  if (systemStatus.value === "All Systems Nominal") return "success";
  if (systemStatus.value === "Degraded") return "warning";
  return "error";
});

type BadgeType = "neutral" | "live" | "warn";

const badgeColor = (type: BadgeType) => {
  if (type === "live") return "success";
  if (type === "warn") return "error";
  return "neutral";
};

const sections = [
  {
    badges: [{ label: "Users", type: "neutral" as BadgeType }],
    chapter: "Chapter I",
    description:
      "Manage accounts, revoke access, and oversee membership records across the system.",
    icon: "mdi:account-multiple",
    id: "users",
    route: "/dashboard/admin/user-registry",
    tags: ["Accounts", "Permissions", "Removal"],
    title: "User Registry",
  },
  {
    badges: [{ label: "Live", type: "live" as BadgeType }],
    chapter: "Chapter II",
    description:
      "Inspect per-user storage consumption, quotas, and archive distribution across volumes.",
    icon: "mdi:archive-outline",
    id: "storage",
    route: "/admin/storage",
    tags: ["Quotas", "Usage", "Volumes"],
    title: "Storage Ledger",
  },
  {
    badges: [{ label: "Filterable", type: "neutral" as BadgeType }],
    chapter: "Chapter III",
    description:
      "Browse and filter the full audit trail by user, event type, and time range.",
    icon: "mdi:book-open-page-variant-outline",
    id: "logs",
    route: "/admin/logs",
    tags: ["Audit", "Events", "Timeline"],
    title: "Activity Chronicles",
  },
  {
    badges: [
      { label: "Server", type: "neutral" as BadgeType },
      { label: "DB", type: "neutral" as BadgeType },
      { label: "Garage", type: "neutral" as BadgeType },
    ],
    chapter: "Chapter IV",
    description:
      "Monitor server health, database integrity, Garage metrics, and request success ratios.",
    icon: "mdi:heart-pulse",
    id: "status",
    route: "/dashboard/admin/service-status",
    tags: ["Health", "Uptime", "Ratios"],
    title: "System Vitals",
  },
  {
    badges: [{ label: "Sessions", type: "live" as BadgeType }],
    chapter: "Chapter V",
    description:
      "Visualise session origins, geographic distribution, and active login sessions.",
    icon: "mdi:map-marker-radius-outline",
    id: "clients",
    route: "/admin/clients",
    tags: ["Geography", "Sessions", "IP Tracking"],
    title: "Client Atlas",
  },
  {
    badges: [{ label: "Global", type: "warn" as BadgeType }],
    chapter: "Chapter VI",
    description:
      "Govern application-level feature flags, capability toggles, and system-wide policies.",
    icon: "mdi:tune-vertical",
    id: "settings",
    route: "/dashboard/admin/system-configuration",
    tags: ["Features", "Policies", "Toggles"],
    title: "System Configuration",
  },
] as const;
</script>

<style scoped>
.nav-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 16px color-mix(in srgb, currentColor 5%, transparent);
}
.nav-card:active {
  transform: translateY(0);
}
</style>
