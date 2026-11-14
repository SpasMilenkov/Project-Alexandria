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
            { label: "Introduction", slug: "getting-started/introduction" },
            { label: "Installation", slug: "getting-started/installation" },
            {
              label: "Manual Installation",
              slug: "getting-started/manual-installation",
            },
            { label: "First Steps", slug: "getting-started/first-steps" },
          ],
        },

        {
          label: "Guides",

          items: [
            { label: "Files & Versioning", slug: "guides/files-and-versioning" },
            { label: "Uploads & Previews", slug: "guides/uploads-and-previews" },
            { label: "Sharing Links", slug: "guides/sharing-links" },
            {
              label: "Tags & Auto-Tagging",
              slug: "guides/tags-and-auto-tagging",
            },
            { label: "Access Policies", slug: "guides/access-policies" },
            {
              label: "Streaming & Playlists",
              slug: "guides/streaming-and-playlists",
            },
            { label: "Encryption", slug: "guides/encryption" },
            {
              label: "Settings & Appearance",
              slug: "guides/settings-and-appearance",
            },

            {
              label: "Development Setup",
              slug: "guides/set-up-development-environment",
            },
            { label: "Operations and Backups", slug: "guides/operations" },
          ],
        },

        {
          label: "Operations",
          items: [{ autogenerate: { directory: "operations" } }],
        },

        {
          label: "Development",
          items: [{ autogenerate: { directory: "development" } }],
        },

        {
          label: "Reference",
          items: [{ autogenerate: { directory: "reference" } }],
        },
      ],
    }),
  ],
});
