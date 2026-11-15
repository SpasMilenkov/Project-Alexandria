import { describe, expect, it } from "vitest";
import { effectScope } from "vue";

import { useSearchQuery } from "@/composables/search/useSearchQuery";
import { formatShownOfTotal } from "@/utils/count";

const setup = () => effectScope().run(() => useSearchQuery())!;

describe("useSearchQuery", () => {
  it("turns filters into chips with readable values", () => {
    const query = setup();

    query.addFilter({ key: "type", value: "files" });
    query.addFilter({ key: "tag", name: "travel", tagId: "t1" });

    expect(query.chips.value.map((chip) => `${chip.key}:${chip.value}`)).toEqual([
      "type:files",
      "tag:travel",
    ]);
  });

  it("leaves an unfinished filter out of the name", () => {
    const query = setup();

    query.text.value = "paris in:Pho";

    expect(query.nameText.value).toBe("paris");
    expect(query.textWithoutTrigger()).toBe("paris ");
  });

  it("adds a separating space before the next key", () => {
    const query = setup();

    query.text.value = "paris";
    expect(query.textForNextToken()).toBe("paris ");

    query.text.value = "";
    expect(query.textForNextToken()).toBe("");
  });

  it("drops only the unfinished filter", () => {
    const query = setup();

    query.text.value = "paris tag:tra";
    query.dropTrigger();

    expect(query.text.value).toBe("paris ");
  });

  it("removes the last chip and reports when there was none", () => {
    const query = setup();

    query.addFilter({ key: "type", value: "files" });
    query.addFilter({ key: "is", value: "deleted" });

    expect(query.removeLastFilter()).toBe(true);
    expect(query.filters.value).toEqual([{ key: "type", value: "files" }]);

    query.clearFilters();
    expect(query.removeLastFilter()).toBe(false);
  });

  it("counts the name or any chip as something to search for", () => {
    const query = setup();

    expect(query.hasCriteria.value).toBe(false);

    query.addFilter({ key: "is", value: "starred" });
    expect(query.hasCriteria.value).toBe(true);
  });
});

describe("formatShownOfTotal", () => {
  it("shows the total only when part of it is listed", () => {
    expect(formatShownOfTotal(20, 134)).toBe("20 of 134");
    expect(formatShownOfTotal(3, 3)).toBe("3");
    expect(formatShownOfTotal(5, undefined)).toBe("5");
  });
});
