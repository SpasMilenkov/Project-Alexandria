import { describe, expect, it } from "vitest";
import { effectScope } from "vue";

import { useVideoEmbedClaim } from "@/composables/useVideoEmbedClaim";

const embed = () => {
  const scope = effectScope();
  const claim = scope.run(() => useVideoEmbedClaim());

  if (!claim) throw new Error("The claim needs an effect scope");

  return { claim, scope };
};

describe("useVideoEmbedClaim", () => {
  it("gives the player to one embed at a time", () => {
    const first = embed();
    const second = embed();

    first.claim.claim();
    expect(first.claim.isOwner.value).toBe(true);
    expect(second.claim.isFree.value).toBe(false);

    second.claim.claim();
    expect(first.claim.isOwner.value).toBe(false);
    expect(second.claim.isOwner.value).toBe(true);

    first.scope.stop();
    second.scope.stop();
  });

  it("lets only the holder release it", () => {
    const holder = embed();
    const other = embed();

    holder.claim.claim();
    other.claim.release();
    expect(holder.claim.isOwner.value).toBe(true);

    holder.claim.release();
    expect(other.claim.isFree.value).toBe(true);

    holder.scope.stop();
    other.scope.stop();
  });

  it("frees the player when the holding embed goes away", () => {
    const holder = embed();
    const other = embed();

    holder.claim.claim();
    holder.scope.stop();
    expect(other.claim.isFree.value).toBe(true);

    other.scope.stop();
  });
});
