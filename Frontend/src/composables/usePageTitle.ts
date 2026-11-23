import { type MaybeRefOrGetter, computed, onScopeDispose, shallowRef, toValue, watchEffect } from "vue";

interface PageTitleClaim {
  owner: symbol;
  title: string;
}

const claim = shallowRef<PageTitleClaim | null>(null);

/** The title a mounted page asked for in place of its route's, or null when none did. */
export const pageTitleOverride = computed(() => claim.value?.title ?? null);

/**
 * Lends the tab title a name the route can't know, like the open note's. Only the page that set
 * the title clears it, so a page mounting before the previous one unmounts keeps its own.
 */
export const usePageTitle = (title: MaybeRefOrGetter<string | null | undefined>) => {
  const owner = Symbol("page-title");

  const release = () => {
    if (claim.value?.owner === owner) claim.value = null;
  };

  watchEffect(() => {
    const value = toValue(title)?.trim();

    if (value) {
      claim.value = { owner, title: value };
      return;
    }

    release();
  });

  onScopeDispose(release);
};
