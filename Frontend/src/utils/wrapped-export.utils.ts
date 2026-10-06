import type { WrappedCardResponse, WrappedDeckResponse } from "@/api/stats";

import { WrappedCardType as Type } from "@/enums/wrapped-card-type";
import { WrappedRhythmEvidence, WrappedTimeScene } from "@/enums/wrapped-story";
import {
  bounded,
  buildWrappedRecipe,
  factsFor,
  formatWrappedDuration,
  monthLabel,
} from "@/utils/wrapped-art.utils";
import { wrappedCardSubline, wrappedStoryNote } from "@/utils/wrapped-copy.utils";
import { type WrappedPalette, wrappedExportColors } from "@/utils/wrapped-palette.utils";
import { monthlyLandscape, rankedRecords, shareLabel } from "@/utils/wrapped-ranking.utils";
import { drawReturnArt } from "@/utils/wrapped-return-draw.utils";
import { buildWrappedSections } from "@/utils/wrapped-sections.utils";
import { getWrappedSkyPreset, skyPresets, skyRidge } from "@/utils/wrapped-sky.utils";
import {
  calendarMonths,
  dateLabel,
  replayMarks,
  returnGeometry,
  vinylGroove,
  waveformDays,
} from "@/utils/wrapped-story.utils";

export type WrappedExportFormat = "png" | "jpeg";

export const exportFilename = (year: number, format: WrappedExportFormat): string =>
  `alexandria-wrapped-${year}.${format}`;

// Break long filenames as well as prose; canvas does not wrap text itself.
export const wrapExportText = (
  text: string,
  width: number,
  measure: (text: string) => number,
): string[] => {
  const lines: string[] = [];

  for (const paragraph of text.split(/\r?\n/)) {
    let line = "";

    for (const word of paragraph.split(/\s+/).filter(Boolean)) {
      const candidate = line ? `${line} ${word}` : word;

      if (measure(candidate) <= width) {
        line = candidate;
        continue;
      }

      if (line) {
        lines.push(line);
        line = "";
      }

      for (const char of Array.from(word)) {
        if (line && measure(line + char) > width) {
          lines.push(line);
          line = "";
        }

        line += char;
      }
    }

    if (line) lines.push(line);
  }

  return lines;
};

export const renderWrappedExport = async (
  response: WrappedDeckResponse,
  isDark: boolean,
  palette: WrappedPalette | null = null,
): Promise<HTMLCanvasElement> => {
  if (!response.deck.cards.length) throw new Error("There is no listening to export yet.");

  await document.fonts.ready;

  const canvas = document.createElement("canvas");

  canvas.width = 1080;
  canvas.height = 1;

  const ctx = canvas.getContext("2d");

  if (!ctx) throw new Error("Your browser could not create an image canvas.");

  const colors = wrappedExportColors(isDark, palette);
  const sections = buildWrappedSections(response.deck.cards);
  const year = new Date(response.from).getUTCFullYear();

  const ink = (value: string): string => {
    const key = value.replace("var(--wrapped-", "").replace(")", "");

    if (key === "cutout") return colors.panel;
    if (key === "pin") return colors.text;
    if (key === "track") return colors.line;

    return (colors as Record<string, string>)[key] ?? value;
  };

  const text = (
    value: string,
    x: number,
    y: number,
    width: number,
    size = 24,
    color = colors.text,
    weight = 400,
    align: CanvasTextAlign = "left",
  ): number => {
    ctx.font = `${weight} ${size}px system-ui, sans-serif`;
    ctx.textBaseline = "top";
    ctx.textAlign = align;
    ctx.fillStyle = color;

    const lines = wrapExportText(value, width, (value) => ctx.measureText(value).width);
    let drawX = x;

    if (align === "center") {
      drawX = x + width / 2;
    } else if (align === "right") {
      drawX = x + width;
    }

    lines.forEach((line, index) => ctx.fillText(line, drawX, y + index * size * 1.32));
    ctx.textAlign = "left";

    return y + lines.length * size * 1.32;
  };

  const circle = (x: number, y: number, radius: number, fill: string, stroke?: string) => {
    ctx.beginPath();
    ctx.arc(x, y, Math.max(0, radius), 0, Math.PI * 2);
    ctx.fillStyle = fill;
    ctx.fill();

    if (stroke) {
      ctx.strokeStyle = stroke;
      ctx.lineWidth = 1;
      ctx.stroke();
    }
  };

  const path = (d: string, fill: string | null, stroke?: string, width = 1) => {
    const shape = new Path2D(d);

    if (fill) {
      ctx.fillStyle = fill;
      ctx.fill(shape);
    }

    if (stroke) {
      ctx.strokeStyle = stroke;
      ctx.lineWidth = width;
      ctx.stroke(shape);
    }
  };

  const line = (x: number, y: number, width: number) => {
    ctx.fillStyle = colors.line;
    ctx.fillRect(x, y, width, 1);
  };

  const panel = (x: number, y: number, width: number, height: number) => {
    ctx.fillStyle = colors.panel;
    ctx.fillRect(x, y, width, height);
  };

  const sky = (
    scene: WrappedTimeScene,
    x: number,
    y: number,
    width: number,
    height: number,
    compact: boolean,
  ) => {
    const cfg = getWrappedSkyPreset(scene, palette);

    ctx.save();
    ctx.beginPath();
    ctx.rect(x, y, width, height);
    ctx.clip();
    ctx.translate(x, y);

    const sourceWidth = compact ? 340 : 900;

    ctx.scale(width / sourceWidth, height / 340);

    if (compact) ctx.translate(-Math.max(0, Math.min(560, cfg.sunX - 170)), 0);

    const gradient = ctx.createLinearGradient(0, 0, 0, 340);

    cfg.skyStops.forEach((stop) =>
      gradient.addColorStop(parseFloat(stop.offset) / 100, stop.color),
    );

    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, 900, 340);

    if (cfg.stars) {
      let seed = cfg.seed ?? 7;

      const random = () => {
        seed |= 0;
        seed = (seed + 0x6d2b79f5) | 0;

        let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);

        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;

        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
      };

      for (let i = 0; i < (cfg.starCount ?? 60); i++) {
        const sx = random() * 900,
          sy = random() * 340 * (cfg.starMaxY ?? 0.6),
          radius = 0.4 + random() * 1.2;

        ctx.globalAlpha = 0.25 + random() * 0.6;
        circle(sx, sy, radius, "#fff");
      }
    }

    ctx.globalAlpha = 0.16;
    circle(cfg.sunX, cfg.sunY, cfg.glowR2, cfg.glowColor);
    ctx.globalAlpha = 0.32;
    circle(cfg.sunX, cfg.sunY, cfg.glowR1, cfg.glowColor);
    ctx.globalAlpha = 1;
    circle(cfg.sunX, cfg.sunY, cfg.sunR, cfg.sunColor);
    ctx.globalAlpha = 0.85;
    path(skyRidge(cfg.baseY - cfg.ridgeGap, 26, 2.6, cfg.seedA, 14, 4.4, cfg.seedB), cfg.ridgeBack);
    ctx.globalAlpha = 1;
    path(skyRidge(cfg.baseY, 34, 2.1, cfg.seedC, 16, 5.1, cfg.seedD), cfg.ridgeFront);
    ctx.restore();
  };

  const rankings = (card: WrappedCardResponse, x: number, y: number, width: number): number => {
    const tracks = card.type === Type.TopSongs;

    let cursor =
      text(tracks ? "THE MIXTAPE" : "YOUR RECORD COLLECTION", x, y, width, 20, colors.muted, 600) +
      24;

    const records = rankedRecords(card.entries);

    for (const { entry, ratio, radius } of records) {
      const top = cursor;

      if (!tracks) {
        circle(x + 40, top + 46, radius * 0.9, colors.teal);

        for (const groove of [0.6, 0.73, 0.86])
          circle(x + 40, top + 46, radius * 0.9 * groove, "transparent", colors.panel);

        circle(x + 40, top + 46, radius * 0.3, colors.panel);
        circle(x + 40, top + 46, radius * 0.05, colors.text);
      } else text(String(entry.rank).padStart(2, "0"), x, top, 64, 32, colors.blue, 600);

      const left = x + 96;
      const available = width - 96;

      cursor = text(entry.title, left, top, available, 26, colors.text, 700) + 8;

      if (entry.artist && tracks)
        cursor = text(entry.artist, left, cursor, available, 21, colors.muted) + 8;

      if (entry.subtitle)
        cursor = text(entry.subtitle, left, cursor, available, 19, colors.muted) + 8;

      cursor =
        text(
          `${shareLabel(entry.share)} of listening`,
          left,
          cursor,
          available,
          20,
          colors.teal,
          600,
        ) + 12;

      if (tracks) {
        ctx.fillStyle = colors.line;
        ctx.fillRect(left, cursor, available, 5);
        ctx.fillStyle = colors.blue;
        ctx.fillRect(left, cursor, available * ratio, 5);
        cursor += 10;
      }

      cursor = Math.max(cursor, top + 100) + 20;
      line(x, cursor, width);
      cursor += 20;
    }

    return cursor;
  };

  const artwork = (
    card: WrappedCardResponse,
    x: number,
    y: number,
    width: number,
    height: number,
  ) => {
    const facts = factsFor(card);

    ctx.save();
    ctx.beginPath();
    ctx.rect(x, y, width, height);
    ctx.clip();
    ctx.translate(x, y);

    if (card.type === Type.MostReplayed) {
      ctx.scale(width / 360, height / 360);

      const tally = replayMarks(facts.count);

      tally.marks.forEach((mark) =>
        path(`M ${mark.x1} ${mark.y1} L ${mark.x2} ${mark.y2}`, null, colors.purple, 4),
      );

      circle(180, 180, 82, "transparent", colors.line);
      ctx.font = "700 50px system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillStyle = colors.text;
      ctx.fillText(String(facts.count), 180, 182);
    } else if (card.type === Type.Bookends) {
      ctx.scale(width / 360, height / 360);

      const groove = vinylGroove(facts, response.from, response.to);

      circle(180, 180, 153, colors.background, colors.line);
      groove.segments.forEach((segment) => path(segment.path, null, colors.purple, segment.width));
      circle(180, 180, 28, colors.clay);
      circle(180, 180, 4, colors.panel);
      circle(groove.first.x, groove.first.y, 6, colors.blue);

      if (!groove.single)
        path(`M ${groove.last.x} ${groove.last.y - 7} l 7 7 l -7 7 l -7 -7 Z`, colors.rose);
    } else if (card.type === Type.BusiestDay) {
      const days = waveformDays(facts.series, facts.from, false);
      const max = Math.max(1, ...facts.series.map((day) => day.seconds));

      line(0, height / 2, width);

      days.forEach((day, i) => {
        const amplitude = (day.seconds / max) * height * 0.82;
        const pitch = width / Math.max(1, days.length);

        ctx.fillStyle = day.date === facts.from ? colors.rose : colors.blue;
        ctx.fillRect(i * pitch, (height - amplitude) / 2, pitch * 0.65, amplitude);
      });
    } else if (card.type === Type.Streak) {
      const months = calendarMonths(facts.series, response.from, response.to, facts.from, facts.to);
      const max = Math.max(1, ...facts.series.map((day) => day.seconds));

      const columns = 3,
        slot = width / columns;

      const monthHeight = height / Math.max(1, Math.ceil(months.length / columns));

      months.forEach((month, i) => {
        const left = (i % columns) * slot,
          top = Math.floor(i / columns) * monthHeight;

        text(
          monthLabel({ date: month.key, seconds: 0, plays: 0 }),
          left,
          top,
          slot,
          12,
          colors.muted,
        );

        const size = Math.min((slot - 8) / 7, (monthHeight - 18) / 6);

        month.cells.forEach((cell, index) => {
          if (!cell?.inRange) return;

          const cx = left + (index % 7) * size,
            cy = top + 17 + Math.floor(index / 7) * size;

          ctx.fillStyle = colors.line;
          ctx.fillRect(cx, cy, size - 2, size - 2);

          if (cell.seconds > 0) {
            ctx.globalAlpha = 0.3 + 0.7 * Math.sqrt(cell.seconds / max);
            ctx.fillStyle = colors.blue;
            ctx.fillRect(cx, cy, size - 2, size - 2);
            ctx.globalAlpha = 1;
          }

          if (cell.streak) {
            ctx.strokeStyle = colors.text;
            ctx.lineWidth = 1;
            ctx.strokeRect(cx, cy, size - 2, size - 2);
          }
        });
      });
    } else {
      const recipe = buildWrappedRecipe(card, "export");

      if (!recipe) {
        ctx.restore();

        return;
      }

      ctx.scale(width / 800, height / 360);

      recipe.paths.forEach((p) =>
        path(
          p.d,
          p.fill === "none" ? null : ink(p.fill),
          p.stroke ? ink(p.stroke) : undefined,
          p.width,
        ),
      );

      recipe.circles.forEach((c) =>
        circle(
          c.x,
          c.y,
          c.r,
          c.fill === "none" ? "transparent" : ink(c.fill),
          c.stroke ? ink(c.stroke) : undefined,
        ),
      );
    }

    ctx.restore();
  };

  const story = (card: WrappedCardResponse, y: number): number => {
    const facts = factsFor(card);
    let cursor = y + 24;

    let left = 404,
      width = 620;

    if (card.type === Type.Persona) {
      const rhythm = facts.rhythm;

      if (
        rhythm?.evidence === WrappedRhythmEvidence.Pronounced &&
        rhythm.scene != null &&
        skyPresets[rhythm.scene]
      )
        sky(rhythm.scene, 56, cursor, 968, 280, false);
      else
        [
          WrappedTimeScene.Morning,
          WrappedTimeScene.Midday,
          WrappedTimeScene.Afternoon,
          WrappedTimeScene.Evening,
          WrappedTimeScene.Night,
        ].forEach((scene, i) => sky(scene, 56 + i * 193.6, cursor, 193.6, 220, true));

      cursor += 304;
      left = 56;
      width = 968;
    } else if (card.type === Type.ReturningFavorite) {
      // Wide graph, so it gets the full width like the Persona sky.
      const geometry = returnGeometry(facts.from, facts.to);

      const drawn = drawReturnArt(
        ctx,
        geometry,
        { x: 56, y: cursor, width: 968 },
        { signal: colors.rose, quiet: colors.muted, marker: colors.text },
      );

      const days = `${new Intl.NumberFormat().format(facts.count)} ${facts.count === 1 ? "day" : "days"} away`;

      text(
        days,
        56 + geometry.labelLeft * 968 - 160,
        cursor + drawn + 8,
        320,
        28,
        colors.text,
        800,
        "center",
      );

      cursor += drawn + 68;
      left = 56;
      width = 968;
    } else artwork(card, 56, cursor, 300, 280);

    cursor = text(card.headline, left, cursor, width, 32, colors.text, 750) + 16;

    const subline = wrappedCardSubline(card);

    if (subline) cursor = text(subline, left, cursor, width, 23, colors.muted) + 16;

    if (facts.from) {
      let dates = dateLabel(facts.from);

      if (facts.to && facts.to !== facts.from) dates += ` → ${dateLabel(facts.to)}`;

      cursor = text(dates, left, cursor, width, 20, colors.muted) + 12;
    }

    for (const entry of card.entries) {
      cursor = text(entry.title, left, cursor, width, 24, colors.text, 600) + 6;

      const detail = [entry.artist, entry.subtitle].filter(Boolean).join(" · ");

      if (detail) cursor = text(detail, left, cursor, width, 20, colors.muted) + 10;
    }

    const storyNote = wrappedStoryNote(card);

    if (storyNote) cursor = text(storyNote, left, cursor + 4, width, 18, colors.muted) + 8;

    if (card.type === Type.ReturningFavorite)
      cursor =
        text(
          "A quiet stretch, then a familiar favorite.",
          left,
          cursor,
          width,
          18,
          colors.muted,
        ) + 8;

    if (card.type === Type.MostReplayed && replayMarks(facts.count).unit > 1) {
      const tally = replayMarks(facts.count);

      cursor =
        text(
          `Full marks: ${tally.unit} plays each. Final mark: ${tally.marks[tally.marks.length - 1]?.value ?? 0}.`,
          left,
          cursor,
          width,
          18,
          colors.muted,
        ) + 8;
    }

    return Math.max(cursor + 24, y + 328);
  };

  const compose = (): number => {
    ctx.fillStyle = colors.background;
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    let y = text("ALEXANDRIA / YOUR YEAR ON RECORD", 56, 48, 968, 22, colors.muted, 600) + 24;

    y = text(`${year} Wrapped`, 56, y, 968, 80, colors.text, 800) + 16;

    const finalObservedDay = new Date(Date.parse(response.to) - 1).toISOString();

    y =
      text(
        `${dateLabel(response.from)} – ${dateLabel(finalObservedDay)}`,
        56,
        y,
        968,
        22,
        colors.muted,
      ) + 32;

    const summary = response.summary;
    const total = summary?.seconds ?? sections.hero?.facts?.seconds ?? 0;

    y =
      text(`${formatWrappedDuration(total)} with your music`, 56, y, 968, 42, colors.teal, 700) +
      12;

    if (summary)
      y =
        text(
          `${summary.tracks} tracks · ${summary.artists} artists · ${summary.qualifiedPlayCount} plays · ${summary.activeDays} listening days`,
          56,
          y,
          968,
          24,
          colors.muted,
        ) + 24;

    const months = monthlyLandscape(sections.hero?.facts?.series ?? []);

    if (months.length) {
      const pitch = 968 / months.length;

      months.forEach((peak, index) => {
        ctx.save();
        ctx.translate(56 + index * pitch, y);
        ctx.scale(pitch / 100, 1);
        path(peak.path, colors.teal);
        ctx.restore();

        text(
          monthLabel(peak.month),
          56 + index * pitch,
          y + 176,
          pitch,
          17,
          colors.muted,
          600,
          "center",
        );

        text(
          formatWrappedDuration(peak.month.seconds),
          56 + index * pitch,
          y + 201,
          pitch,
          15,
          colors.muted,
          400,
          "center",
        );
      });

      y += 254;
    }

    const comparison = sections.hero?.facts?.comparison;

    if (comparison) {
      y = text(comparison.copy, 56, y, 968, 28, colors.text, 600) + 8;

      if (comparison.description) {
        y = text("At this milestone", 56, y, 968, 17, colors.muted, 600) + 8;
        y = text(comparison.description, 56, y, 968, 22, colors.muted) + 16;
      }

      y =
        text(
          `${comparison.durationLabel} · your listens added up over time`,
          56,
          y,
          968,
          20,
          colors.muted,
        ) + 24;
    }

    line(56, y, 968);
    y += 32;

    const rankingTop = y;

    let artistsEnd = y,
      tracksEnd = y;

    if (sections.artists) artistsEnd = rankings(sections.artists, 56, rankingTop, 460);

    if (sections.songs)
      tracksEnd = rankings(
        sections.songs,
        sections.artists ? 564 : 56,
        rankingTop,
        sections.artists ? 460 : 968,
      );

    y = Math.max(artistsEnd, tracksEnd) + 16;

    for (const card of sections.spotlight) {
      // Measure before painting the opaque paper behind a variable-length story.
      const end = story(card, y);

      panel(32, y, 1016, end - y);
      story(card, y);
      y = end + 24;
    }

    y =
      text(
        "Made from your music in Alexandria. Every year has its own sound.",
        56,
        y + 8,
        968,
        22,
        colors.muted,
      ) + 16;

    return y + 40;
  };

  const height = Math.ceil(compose());

  if (height > 12000)
    throw new Error("This recap is too tall to export safely. Try a shorter listening period.");

  canvas.height = height;
  compose();

  return canvas;
};

export const encodeWrappedExport = (
  canvas: HTMLCanvasElement,
  format: WrappedExportFormat,
): Promise<Blob> =>
  new Promise((resolve, reject) => {
    const mime = format === "jpeg" ? "image/jpeg" : "image/png";

    canvas.toBlob(
      (blob) => {
        if (!blob || blob.type !== mime) {
          reject(new Error("Your browser could not encode this image format."));

          return;
        }

        resolve(blob);
      },
      mime,
      0.94,
    );
  });
