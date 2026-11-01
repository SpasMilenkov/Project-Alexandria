import { describe, expect, it } from "vitest";

import { buildRingArcs } from "@/utils/storage-ring";

const GB = 1_000_000_000;

const segments = (files: number, previews: number, transcoded: number) => [
  { color: "files", key: "files", value: files },
  { color: "previews", key: "previews", value: previews },
  { color: "transcoded", key: "transcoded", value: transcoded },
];

const end = (arc: { offset: number; length: number }) => arc.offset + arc.length;

describe("buildRingArcs", () => {
  it("returns nothing without a quota or without usage", () => {
    expect(buildRingArcs(segments(1, 1, 1), 0)).toEqual([]);
    expect(buildRingArcs(segments(0, 0, 0), 10 * GB)).toEqual([]);
  });

  it("fills the used share of the ring in segment order, separated by gaps", () => {
    const arcs = buildRingArcs(segments(4 * GB, 2 * GB, 2 * GB), 10 * GB, {
      gap: 0.01,
      minLength: 0,
    });

    expect(arcs.map((arc) => arc.key)).toEqual(["files", "previews", "transcoded"]);
    expect(arcs[0].offset).toBe(0);
    expect(arcs[1].offset).toBeCloseTo(end(arcs[0]) + 0.01);
    expect(arcs[2].offset).toBeCloseTo(end(arcs[1]) + 0.01);
    expect(end(arcs[2])).toBeCloseTo(0.8);
    expect(arcs[0].length).toBeCloseTo(arcs[1].length * 2);
  });

  it("skips empty segments", () => {
    const arcs = buildRingArcs(segments(5 * GB, 0, GB), 10 * GB);

    expect(arcs.map((arc) => arc.key)).toEqual(["files", "transcoded"]);
  });

  it("raises a sliver to the minimum and takes the difference from the largest segment", () => {
    const arcs = buildRingArcs(segments(6 * GB, 0.001 * GB, GB), 10 * GB, {
      gap: 0.01,
      minLength: 0.03,
    });
    const used = arcs.reduce((sum, arc) => sum + arc.length, 0) + 0.02;

    expect(arcs[1].length).toBeCloseTo(0.03);
    expect(used).toBeCloseTo(0.7001);
  });

  it("closes a full ring with a gap after every segment", () => {
    const arcs = buildRingArcs(segments(6 * GB, 2 * GB, 2 * GB), 10 * GB, {
      gap: 0.01,
      minLength: 0,
    });

    expect(end(arcs[2]) + 0.01).toBeCloseTo(1);
  });

  it("draws a single segment at full quota as a seamless circle", () => {
    const arcs = buildRingArcs(segments(10 * GB, 0, 0), 10 * GB, { gap: 0.01 });

    expect(arcs).toEqual([{ color: "files", key: "files", length: 1, offset: 0 }]);
  });

  it("caps usage above the quota at a full ring", () => {
    const arcs = buildRingArcs(segments(9 * GB, 2 * GB, 2 * GB), 10 * GB, {
      gap: 0.01,
      minLength: 0,
    });

    expect(end(arcs[2]) + 0.01).toBeCloseTo(1);
  });
});
