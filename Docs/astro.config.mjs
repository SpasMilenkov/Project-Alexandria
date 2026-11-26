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
      logo: { src: "./src/assets/alexandria-logo.svg" },
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
            { label: "Using the Builder", slug: "guides/using-the-builder" },
            {
              label: "Manual Installation",
              slug: "getting-started/manual-installation",
            },
            { label: "First Steps", slug: "getting-started/first-steps" },
          ],
        },

        {
          label: "Using Alexandria",

          items: [
            { label: "Files & Versioning", slug: "guides/files-and-versioning" },
            { label: "Uploads & Previews", slug: "guides/uploads-and-previews" },
            {
              label: "Search & Documents",
              slug: "guides/search-and-documents",
            },
            { label: "Notes", slug: "guides/notes" },
            { label: "Gallery & Photos", slug: "guides/gallery-and-photos" },
            {
              label: "Streaming & Playlists",
              slug: "guides/streaming-and-playlists",
            },
            { label: "Sharing Links", slug: "guides/sharing-links" },
            { label: "Encryption", slug: "guides/encryption" },
            {
              label: "Settings & Appearance",
              slug: "guides/settings-and-appearance",
            },
          ],
        },

        {
          label: "Automation & Data",

          items: [
            { label: "Automating Your Library", slug: "guides/automation" },
            {
              label: "Tags & Auto-Tagging",
              slug: "guides/tags-and-auto-tagging",
            },
            { label: "Access Policies", slug: "guides/access-policies" },
            { label: "Exporting Your Data", slug: "guides/exporting-your-data" },
          ],
        },

        {
          label: "Administration",

          items: [
            { label: "Admin Area", slug: "guides/administration" },
            { label: "Operations Overview", slug: "guides/operations" },
          ],
        },

        {
          label: "Operations",
          items: [{ autogenerate: { directory: "operations" } }],
        },

        {
          label: "Development",
          items: [
            {
              label: "Development Setup",
              slug: "guides/set-up-development-environment",
            },
            { autogenerate: { directory: "development" } },
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
