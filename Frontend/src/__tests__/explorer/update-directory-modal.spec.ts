import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import UpdateDirectoryModal from "@/components/dashboard/file-system/Modals/UpdateDirectoryModal.vue";

vi.mock("@/mutations/directories", () => ({
  updateDirectory: () => ({ mutateAsync: vi.fn(), state: ref({}) }),
}));

const modalStub = {
  name: "ModalStub",
  template: '<div class="modal-stub"><slot name="body" /></div>',
};

describe("UpdateDirectoryModal", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const inputValue = (currentName: string) => {
    const wrapper = mount(UpdateDirectoryModal, {
      global: {
        plugins: [createPinia()],
        stubs: { Modal: modalStub, UModal: modalStub },
      },
      props: { currentName, directoryId: "d1" },
    });
    const input = wrapper.find("input");
    expect(input.exists()).toBe(true);
    return (input.element as HTMLInputElement).value;
  };

  it("prefills the input with the current directory name", () => {
    expect(inputValue("Projects")).toBe("Projects");
  });

  it("falls back to an empty input when the current name is unknown", () => {
    expect(inputValue("")).toBe("");
  });
});
