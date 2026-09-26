import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { DirectoryPolicyDto } from "@/api/policy";

import PolicySection from "@/components/policy/PolicySection.vue";

const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
  useQueryCache: vi.fn(() => ({ invalidateQueries: vi.fn() })),
}));

vi.mock("@/queries/policies", () => ({
  getPolicyByDirectory: vi.fn(),
}));

const createFn = vi.fn();
const deleteFn = vi.fn();
vi.mock("@/mutations/policies", () => ({
  createPolicy: () => ({ isLoading: ref(false), mutateAsync: createFn }),
  deletePolicy: () => ({ isLoading: ref(false), mutateAsync: deleteFn }),
  updatePolicy: () => ({ isLoading: ref(false), mutateAsync: vi.fn() }),
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

describe("PolicySection automation setup", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockUseQuery.mockReset();
    createFn.mockReset();
    deleteFn.mockReset();
    errorToast.mockClear();
    successToast.mockClear();
    mockUseQuery.mockReturnValue({ data: ref<DirectoryPolicyDto | null>(null), isLoading: ref(false) });
  });

  const mountSection = () =>
    mount(PolicySection, {
      global: {
        plugins: [createPinia()],
        stubs: { PolicyRuleModal: true, PolicyRuleRow: true },
      },
      props: { directoryId: "d1" },
    });

  const clickByText = async (wrapper: ReturnType<typeof mountSection>, text: string) => {
    const buttons = wrapper.findAll("button");
    const target = buttons.find((button) => button.text().includes(text));
    expect(target).toBeDefined();
    await target!.trigger("click");
  };

  it("shows the empty state immediately when no policy exists", () => {
    const wrapper = mountSection();
    expect(wrapper.find(".animate-spin").exists()).toBe(false);
    expect(wrapper.text()).toContain("No automation configured");
    expect(wrapper.text()).toContain("Set up automation");
  });

  it("creates the policy and confirms success", async () => {
    createFn.mockResolvedValue({ id: "p1" });
    const wrapper = mountSection();
    await clickByText(wrapper, "Set up automation");
    expect(createFn).toHaveBeenCalledWith({ directoryId: "d1", inheritedByChildren: false });
    expect(successToast).toHaveBeenCalledWith("Automation enabled");
    expect(errorToast).not.toHaveBeenCalled();
  });

  it("surfaces create failures instead of failing silently", async () => {
    createFn.mockRejectedValue(new Error("boom"));
    const wrapper = mountSection();
    await clickByText(wrapper, "Set up automation");
    expect(errorToast).toHaveBeenCalledWith("Failed to enable automation", expect.any(Error));
    expect(successToast).not.toHaveBeenCalled();
  });
});
