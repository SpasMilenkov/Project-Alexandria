import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import {
  AudioRung,
  PolicyActionType,
  type PolicyRuleDto,
  PolicyTriggerType,
  VideoRung,
} from "@/api/policy";
import PolicyRuleModal from "@/components/policy/PolicyRuleModal.vue";

vi.mock("@/mutations/policies", () => ({
  addRule: () => ({ isLoading: ref(false), mutateAsync: vi.fn() }),
  updateRule: () => ({ isLoading: ref(false), mutateAsync: vi.fn() }),
}));

const modalStub = {
  name: "ModalStub",
  template: '<div class="modal-stub"><slot name="body" /></div>',
};

const transcodeRule: PolicyRuleDto = {
  actionType: PolicyActionType.Transcode,
  applyOnNewVersion: false,
  createdAt: "2026-11-01T00:00:00Z",
  id: "rule-1",
  parameters: {
    audioRungs: [AudioRung.Kbps192],
    generateThumbnail: false,
    videoRungs: [VideoRung.P720],
  },
  policyId: "policy-1",
  priority: 0,
  triggerType: PolicyTriggerType.AnyFile,
  triggerValue: "*",
  updatedAt: "2026-11-01T00:00:00Z",
};

const mountModal = (rule?: PolicyRuleDto) =>
  mount(PolicyRuleModal, {
    global: {
      plugins: [createPinia()],
      stubs: { Modal: modalStub, UModal: modalStub },
    },
    props: { directoryId: "dir-1", open: true, policyId: "policy-1", rule },
  });

describe("PolicyRuleModal", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("shows the locked action without a picker when editing a rule", () => {
    const wrapper = mountModal(transcodeRule);

    expect(wrapper.text()).toContain("The action can't be changed after a rule is created.");
    expect(wrapper.find('[role="radiogroup"]').exists()).toBe(false);
  });

  it("offers the action picker with Backup unavailable when creating a rule", () => {
    const wrapper = mountModal();

    const options = wrapper.findAll('[role="radio"]');
    const backup = options.find((option) => option.text().includes("Backup"));

    expect(wrapper.find('[role="radiogroup"]').exists()).toBe(true);
    expect(backup?.attributes("disabled")).toBeDefined();
    expect(backup?.text()).toContain("Coming soon");
  });
});
