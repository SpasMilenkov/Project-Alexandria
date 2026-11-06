import type { SiteIconState } from "@/features/site-icon/state";

export interface GlyphData {
  body: string;
  width: number;
  height: number;
}

const SIZE = 32;
const RADIUS = 7;
const GLYPH_SIZE = 22;
const DOT = { cx: 26, cy: 26, gap: 8, r: 5.5 };

const mixWithWhite = (hex: string, amount: number): string => {
  const channels = hex.match(/[\da-f]{2}/giu) ?? [];

  return `#${channels
    .map((pair) => {
      const value = Number.parseInt(pair, 16);
      return Math.round(value + (255 - value) * amount)
        .toString(16)
        .padStart(2, "0");
    })
    .join("")}`;
};

const tileLayer = (state: SiteIconState): string => {
  const tile = `<rect width="${SIZE}" height="${SIZE}" rx="${RADIUS}" fill="${state.tile}"/>`;

  if (state.fill === null) return tile;

  // The unfilled part stays a light shade of the accent so the tab keeps its color mid-upload.
  const height = SIZE * state.fill;
  const track = `<rect width="${SIZE}" height="${SIZE}" rx="${RADIUS}" fill="${mixWithWhite(state.tile, 0.45)}"/>`;
  const level = `<rect y="${SIZE - height}" width="${SIZE}" height="${height}" fill="${state.tile}"/>`;

  return `${track}<g clip-path="url(#t)">${level}</g>`;
};

const glyphLayer = (glyph: GlyphData): string => {
  const offset = (SIZE - GLYPH_SIZE) / 2;
  const scale = GLYPH_SIZE / Math.max(glyph.width, glyph.height);

  // Inlined white instead of currentColor so rasterizers that skip CSS color still draw the glyph.
  const body = glyph.body.replaceAll("currentColor", "#fff");

  return `<g transform="translate(${offset} ${offset}) scale(${scale})">${body}</g>`;
};

export const buildSiteIconSvg = (state: SiteIconState, glyph: GlyphData): string => {
  const defs = [
    `<clipPath id="t"><rect width="${SIZE}" height="${SIZE}" rx="${RADIUS}"/></clipPath>`,
  ];

  // The dot sits in a cut-out so it reads against the browser's tab color, not the tile.
  if (state.dot) {
    defs.push(
      `<mask id="d"><rect width="${SIZE}" height="${SIZE}" fill="#fff"/><circle cx="${DOT.cx}" cy="${DOT.cy}" r="${DOT.gap}" fill="#000"/></mask>`,
    );
  }

  const body = `${tileLayer(state)}${glyphLayer(glyph)}`;
  const content = state.dot
    ? `<g mask="url(#d)">${body}</g><circle cx="${DOT.cx}" cy="${DOT.cy}" r="${DOT.r}" fill="${state.dot}"/>`
    : body;

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${SIZE} ${SIZE}"><defs>${defs.join("")}</defs>${content}</svg>`;
};

export const toDataUri = (svg: string): string =>
  `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
