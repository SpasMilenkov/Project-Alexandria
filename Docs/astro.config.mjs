// @ts-check
import { defineConfig } from "astro/config";
import starlight from "@astrojs/starlight";
import mermaid from 'astro-mermaid';

// https://astro.build/config
export default defineConfig({
  integrations: [
    mermaid({
      theme: 'forest',
      autoTheme: true
    }),
    starlight({
      title: "Alexandria",
      customCss: ["./src/styles/custom.css"],
      social: [
        {
          icon: "github",
          label: "GitHub",
          href: "https://github.com/SpasMilenkov/Project-Alexandria",
        },
      ],
      sidebar: [
        {
          label: "Getting Started",
          items: [
            { label: "Installation", slug: "getting-started/installation" },
            {
              label: "Manual Installation",
              slug: "getting-started/manual-installation",
            },
          ],
        },
        {
          label: "Guides",
          items: [
            {
              label: "Development Setup",
              slug: "guides/set-up-development-environment",
            },

          ],
        },
        {
          label: "Reference",
          items: [{ autogenerate: { directory: "reference" } }],
        },
      ],
    }),
  ],
});
