import { computed, onScopeDispose, shallowRef } from "vue";

const owner = shallowRef<symbol | null>(null);

/**
 * At most one video embed shows a player at a time, even when a note embeds the same video
 * twice: two players on one track would both load and play it. The claim goes away with the
 * embed that holds it.
 */
export const useVideoEmbedClaim = () => {
  const id = Symbol("video-embed");

  const isOwner = computed(() => owner.value === id);
  const isFree = computed(() => owner.value === null);

  const claim = () => {
    owner.value = id;
  };

  const release = () => {
    if (owner.value === id) owner.value = null;
  };

  onScopeDispose(release);

  return { claim, isFree, isOwner, release };
};
