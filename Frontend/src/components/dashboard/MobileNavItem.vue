<template>
  <RouterLink :to="to" custom v-slot="{ navigate, isExactActive }">
    <button
      type="button"
      class="mobile-nav-item"
      :class="{ 'mobile-nav-item--active': isExactActive || active }"
      @click="navigate"
    >
      <span class="mobile-nav-item__icon-wrap">
        <UIcon :name="icon" class="size-4.5" />
      </span>
      <span class="mobile-nav-item__label">{{ label }}</span>
      <UIcon name="i-heroicons-chevron-right" class="size-4 text-muted ml-auto opacity-90" />
    </button>
  </RouterLink>
</template>

<script setup lang="ts">
defineProps<{
  icon: string;
  label: string;
  to: string;
  active?: boolean;
}>();
</script>

<style scoped>
.mobile-nav-item {
  display: flex;
  align-items: center;
  gap: 0.875rem;
  width: 100%;
  padding: 0.875rem 0.875rem;
  border-radius: 0.625rem;
  text-align: left;
  /* inherit theme text colour */
  color: var(--ui-text);
  background: transparent;
  transition:
    background 0.15s ease,
    color 0.15s ease;
  /* minimum 48px touch target per WCAG */
  min-height: 52px;
  cursor: pointer;
  border: none;
  outline: none;
}

.mobile-nav-item:active {
  background: var(--ui-primary) / 0.08;
  transform: scale(0.985);
}

.mobile-nav-item--active {
  background: color-mix(in srgb, var(--ui-primary) 10%, transparent);
  color: var(--ui-primary);
}

.mobile-nav-item--active .mobile-nav-item__icon-wrap {
  background: color-mix(in srgb, var(--ui-primary) 15%, transparent);
  color: var(--ui-primary);
}

.mobile-nav-item--active .mobile-nav-item__label {
  font-weight: 600;
}

.mobile-nav-item__icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border-radius: 0.5rem;
  background: color-mix(in srgb, currentColor 8%, transparent);
  flex-shrink: 0;
  transition:
    background 0.15s ease,
    color 0.15s ease;
}

.mobile-nav-item__label {
  font-size: 0.9375rem;
  font-weight: 500;
  line-height: 1.2;
  flex: 1;
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
