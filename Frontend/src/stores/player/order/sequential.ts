import type { MediaFileDto } from "@/api/streaming";

import { RANGE_SIZE, httpStatus } from "@/api/shuffle";
import {
  type SourceAnchor,
  type SourceDescriptor,
  fetchAnchorPage,
  fetchSequentialPage,
  indexOfAnchor,
} from "@/utils/player-source";

import type { RangeBufferHooks, RangeResult } from "../types";

import { createRangeBuffer } from "./range-buffer";

export interface AnchorLookup {
  position: number;
  total: number;
  offset: number;
  items: { position: number; file: MediaFileDto }[];
}

const toRange = (offset: number, items: MediaFileDto[], total: number): RangeResult => ({
  offset,
  scannedCount: items.length,
  total,
  items: items.map((file, index) => ({ position: offset + index, file })),
});

export const locateAnchor = async (
  ref: SourceDescriptor,
  anchor: SourceAnchor,
): Promise<AnchorLookup | null> => {
  const page = await fetchAnchorPage(ref, anchor).catch((err: unknown) => {
    // A sequential anchor miss is a 404 (X6). Callers fall back or apply PE5.
    if (httpStatus(err) === 404) return null;
    throw err;
  });
  if (!page) return null;
  const index = indexOfAnchor(page.items, anchor);
  if (index === -1) return null;
  const offset = (page.currentPage - 1) * RANGE_SIZE;
  const items = page.items.map((file, itemIndex) => ({ position: offset + itemIndex, file }));
  return { position: offset + index, total: page.totalCount, offset, items };
};

export const createSequentialOrder = (ref: SourceDescriptor, hooks: RangeBufferHooks = {}) =>
  createRangeBuffer(async (start) => {
    const page = await fetchSequentialPage(ref, Math.floor(start / RANGE_SIZE) + 1);
    const offset = (page.currentPage - 1) * RANGE_SIZE;
    return toRange(offset, page.items, page.totalCount);
  }, hooks);
