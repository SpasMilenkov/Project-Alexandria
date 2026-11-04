import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createMemoryHistory, createRouter } from "vue-router";

import SidebarSection from "@/components/dashboard/SidebarSection.vue";
import { buildSidebarSections } from "@/utils/sidebar-navigation";

const NavigationMenu = {
  props: { ariaLabel: String, collapsed: Boolean, items: Array },
  template: `
    <nav :aria-label="ariaLabel" :data-collapsed="collapsed ? 'true' : 'false'">
      <a v-for="item in items" :key="item.to" :data-active="item.active ? 'true' : 'false'">
        {{ item.label }}
      </a>
    </nav>
  `,
};

const stubs = {
  Icon: true,
  NavigationMenu,
  Separator: { template: '<hr data-rule="true" />' },
};

const [library, media] = buildSidebarSections({ isAdmin: false, streamingEnabled: true });

const mountSection = async (props: Record<string, unknown>, path = "/streaming/music") => {
  const page = { template: "<div />" };
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { component: page, name: "home", path: "/home" },
      { component: page, name: "music", path: "/streaming/music" },
    ],
  });

  await router.push(path);

  return mount(SidebarSection, { global: { plugins: [router], stubs }, props });
};

const linksIn = (wrapper: Awaited<ReturnType<typeof mountSection>>, label: string) =>
  wrapper.get(`nav[aria-label="${label}"]`).findAll("a");

describe("SidebarSection", () => {
  it("renders the library without a header", async () => {
    const wrapper = await mountSection({ section: library });

    expect(wrapper.find("button").exists()).toBe(false);
    expect(linksIn(wrapper, "Library navigation")).toHaveLength(5);
  });

  it("highlights only the leaf for the current page", async () => {
    const wrapper = await mountSection({ open: true, section: media });
    const active = linksIn(wrapper, "Media navigation").filter(
      (link) => link.attributes("data-active") === "true",
    );

    expect(active.map((link) => link.text())).toEqual(["Music"]);
    expect(wrapper.find('nav[aria-label="Current Media page"]').exists()).toBe(false);
  });

  it("toggles through the header and reports the new state", async () => {
    const wrapper = await mountSection({ open: true, section: media });
    const header = wrapper.get("button");

    expect(header.attributes("aria-expanded")).toBe("true");
    await header.trigger("click");
    expect(wrapper.emitted("update:open")).toEqual([[false]]);
  });

  it("keeps the current page visible while closed", async () => {
    const wrapper = await mountSection({ open: false, section: media });
    const header = wrapper.get("button");

    expect(header.attributes("aria-expanded")).toBe("false");
    expect(header.text()).toBe("Media");
    expect(linksIn(wrapper, "Current Media page").map((link) => link.text())).toEqual(["Music"]);
  });

  it("shows no peek row when the current page is elsewhere", async () => {
    const wrapper = await mountSection({ open: false, section: media }, "/home");

    expect(wrapper.find('nav[aria-label="Current Media page"]').exists()).toBe(false);
  });

  it("shows every destination as an icon in the rail, even when closed", async () => {
    const wrapper = await mountSection({ collapsed: true, open: false, section: media });
    const nav = wrapper.get('nav[aria-label="Media navigation"]');

    expect(wrapper.find("button").exists()).toBe(false);
    expect(wrapper.find("[data-rule]").exists()).toBe(true);
    expect(nav.attributes("data-collapsed")).toBe("true");
    expect(nav.findAll("a")).toHaveLength(media.items.length);
  });
});
