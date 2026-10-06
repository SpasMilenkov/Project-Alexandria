import type { returnGeometry } from "@/utils/wrapped-story.utils";

type ReturnGeometry = ReturnType<typeof returnGeometry>;

export interface ReturnArtColors {
  signal: string;
  quiet: string;
  marker: string;
}

const diamond = (x: number, y: number, r: number): Path2D => {
  const path = new Path2D();

  path.moveTo(x, y - r);
  path.lineTo(x + r, y);
  path.lineTo(x, y + r);
  path.lineTo(x - r, y);
  path.closePath();

  return path;
};

export const drawReturnArt = (
  ctx: CanvasRenderingContext2D,
  g: ReturnGeometry,
  box: { x: number; y: number; width: number },
  colors: ReturnArtColors,
): number => {
  const scale = box.width / g.width;
  const px = 1 / scale; // one canvas pixel, in geometry units

  ctx.save();
  ctx.translate(box.x, box.y);
  ctx.scale(scale, scale);
  ctx.lineCap = "round";
  ctx.lineJoin = "round";

  // The quiet stretch: short dashes and a bracket.
  ctx.strokeStyle = colors.quiet;
  ctx.lineWidth = 3 * px;
  ctx.setLineDash([3 * px, 6 * px]);
  ctx.stroke(new Path2D(g.gapPath));
  ctx.setLineDash([]);
  ctx.lineWidth = 2 * px;
  ctx.stroke(new Path2D(g.bracketPath));

  // The signal: last play, and the return.
  ctx.strokeStyle = colors.signal;
  ctx.lineWidth = 3 * px;
  ctx.stroke(new Path2D(g.leadPath));
  ctx.stroke(new Path2D(g.returnPath));

  // Markers.
  ctx.fillStyle = colors.signal;
  ctx.fill(diamond(g.lastApex.x, g.lastApex.y, 5 * px));
  ctx.fillStyle = colors.marker;
  ctx.globalAlpha = 0.16;
  ctx.fill(diamond(g.apex.x, g.apex.y, 16 * px));
  ctx.globalAlpha = 1;
  ctx.fill(diamond(g.apex.x, g.apex.y, 7 * px));

  ctx.restore();

  return g.height * scale;
};
