import { describe, expect, it } from "vitest";

import { metadataYearSchema } from "@/schemas/file";

describe("metadataYearSchema", () => {
  it.each(["1999", "2000", String(new Date().getFullYear())])("accepts %s", (year) => {
    expect(metadataYearSchema.safeParse(year).success).toBe(true);
  });

  it.each(["2221", "99", "19999", "abcd", "19 9", "", "   "])("rejects %s", (year) => {
    expect(metadataYearSchema.safeParse(year).success).toBe(false);
  });
});
