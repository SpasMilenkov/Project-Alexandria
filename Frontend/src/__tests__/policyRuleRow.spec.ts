import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import {
  PolicyActionType,
  type PolicyRuleDto,
  PolicyTriggerType,
} from "@/api/policy";
import type * as vueUse from "@vueuse/core";

import PolicyRuleRow from "@/components/policy/PolicyRuleRow.vue";

const deleteFn = vi.fn();
vi.mock("@/mutations/policies", () => ({
  deleteRule: () => ({ mutateAsync: deleteFn }),
}));

const errorToast = vi.fn();
const successToast = vi.fn();
vi.mock("@/composables/useAppToast", () => ({
  useAppToast: () => ({
    error: errorToast,
    info: vi.fn(),
    success: successToast,
    warning: vi.fn(),
  }),
}));

const isMobile = ref(false);
vi.mock("@vueuse/core", async (importOriginal) => {
  const actual = await importOriginal<typeof vueUse>();
  return { ...actual, useBreakpoints: () => ({ smaller: () => isMobile }) };
});

const makeRule = (): PolicyRuleDto => ({
  actionType: PolicyActionType.AutoTag,
  applyOnNewVersion: false,
  createdAt: "2026-01-01T00:00:00.000Z",
  id: "r1",
  parameters: {},
  policyId: "p1",
  priority: 0,
  triggerType: PolicyTriggerType.AnyFile,
  triggerValue: "",
  updatedAt: "2026-01-01T00:00:00.000Z",
});

describe("PolicyRuleRow", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    deleteFn.mockReset();
    errorToast.mockClear();
    successToast.mockClear();
    isMobile.value = false;
  });

  const mountRow = () =>
    mount(PolicyRuleRow, {
      global: { plugins: [createPinia()] },
      props: { directoryId: "d1", rule: makeRule() },
    });

  it("reveals actions on hover and keyboard focus on desktop", () => {
    const actions = mountRow().find(".transition-opacity");
    expect(actions.exists()).toBe(true);
    expect(actions.classes()).toContain("opacity-0");
    expect(actions.classes()).toContain("group-hover:opacity-100");
    expect(actions.classes()).toContain("focus-within:opacity-100");
  });

  it("keeps actions visible on touch layouts with no hover", () => {
    isMobile.value = true;
    const actions = mountRow().find(".transition-opacity");
    expect(actions.exists()).toBe(true);
    expect(actions.classes()).not.toContain("opacity-0");
    expect(actions.classes()).toContain("opacity-100");
  });

  it("deletes a single rule and confirms success", async () => {
    deleteFn.mockResolvedValue(undefined);
    const wrapper = mountRow();
    await wrapper.find('[aria-label="Delete rule"]').trigger("click");
    expect(deleteFn).toHaveBeenCalledWith({ directoryId: "d1", ruleId: "r1" });
    expect(successToast).toHaveBeenCalledWith("Rule removed");
    expect(errorToast).not.toHaveBeenCalled();
  });

  it("surfaces delete failures instead of failing silently", async () => {
    deleteFn.mockRejectedValue(new Error("boom"));
    const wrapper = mountRow();
    await wrapper.find('[aria-label="Delete rule"]').trigger("click");
    expect(errorToast).toHaveBeenCalledWith("Failed to remove rule", expect.any(Error));
    expect(successToast).not.toHaveBeenCalled();
  });
});
