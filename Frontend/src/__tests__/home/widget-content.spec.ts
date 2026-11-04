import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { defineAsyncComponent, defineComponent } from "vue";

import HomeWidgetContent from "@/components/dashboard/home/HomeWidgetContent.vue";
import { defaultHomeLayout } from "@/utils/home-layout";

const ready = defineComponent({ template: "<p>Ready widget</p>" });
const global = { stubs: { UButton: { template: "<button><slot /></button>" }, UIcon: true } };

describe("Home widget content isolation", () => {
  it("shows a centered loading gate until the selected component is ready", async () => {
    let finish = () => {};
    const component = defineAsyncComponent(
      () =>
        new Promise<typeof ready>((resolve) => {
          finish = () => resolve(ready);
        }),
    );
    const wrapper = mount(HomeWidgetContent, {
      props: { component, widget: defaultHomeLayout().widgets[0]! },
      global,
    });

    expect(wrapper.find('[aria-label="Loading widget"]').exists()).toBe(true);
    finish();
    await flushPromises();
    expect(wrapper.text()).toContain("Ready widget");
    expect(wrapper.find('[aria-label="Loading widget"]').exists()).toBe(false);
    wrapper.unmount();
  });

  it("contains a failed component and retries without blocking another widget", async () => {
    const loader = vi
      .fn()
      .mockRejectedValueOnce(new Error("chunk unavailable"))
      .mockResolvedValue(ready);
    const component = defineAsyncComponent(loader);
    const sibling = mount(HomeWidgetContent, {
      props: { component: ready, widget: defaultHomeLayout().widgets[1]! },
      global,
    });
    const wrapper = mount(HomeWidgetContent, {
      props: { component, widget: defaultHomeLayout().widgets[0]! },
      global,
    });
    await flushPromises();

    expect(wrapper.find('[role="alert"]').text()).toContain("This widget could not be loaded.");
    expect(sibling.text()).toContain("Ready widget");
    await wrapper.get("button").trigger("click");
    await flushPromises();
    expect(loader).toHaveBeenCalledTimes(2);
    expect(wrapper.text()).toContain("Ready widget");
    wrapper.unmount();
    sibling.unmount();
  });
});
