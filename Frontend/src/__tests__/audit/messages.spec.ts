import { describe, expect, it } from "vitest";

import {
  getAuditDiff,
  getAuditMessage,
  getAuditMetadataEntries,
} from "@/composables/useAuditMessage";
import { AuditEventCode } from "@/enums";

const ALL_CODES = Object.values(AuditEventCode).filter(
  (value): value is AuditEventCode => typeof value === "number",
);

describe("getAuditMessage", () => {
  it("names every event code", () => {
    const named = ALL_CODES.filter((code) => code !== AuditEventCode.Unknown);

    for (const code of named) {
      expect(getAuditMessage(code), AuditEventCode[code]).not.toBe("Unknown action");
    }
  });

  it("accepts enum names and falls back for unknown ones", () => {
    expect(getAuditMessage("DirectoryRenamed")).toBe("Folder renamed");
    expect(getAuditMessage("NotACode")).toBe("Unknown action");
  });
});

describe("getAuditDiff", () => {
  it("shows folder names for moves, not directory ids", () => {
    const metadata = JSON.stringify({
      new: "Music",
      new_directory_id: "b",
      old: null,
      old_directory_id: null,
    });

    expect(getAuditDiff(AuditEventCode.FileMoved, metadata)).toEqual({
      label: "Location",
      new: "Music",
      old: "Top level",
      type: "diff",
    });
  });

  it("formats quotas as sizes and versions with a v", () => {
    expect(
      getAuditDiff(
        AuditEventCode.UserQuotaChanged,
        JSON.stringify({ new: 2_000_000_000, old: 1_000_000_000 }),
      ),
    ).toMatchObject({ new: "2.00 GB", old: "1.00 GB" });

    expect(
      getAuditDiff(AuditEventCode.FileActiveVersionChanged, '{"old":3,"new":1}'),
    ).toMatchObject({
      new: "v1",
      old: "v3",
    });
  });

  it("has nothing to show for events without old and new values", () => {
    expect(getAuditDiff(AuditEventCode.UserPasswordChanged, '{"changed_at":"x"}')).toEqual({
      type: "none",
    });
    expect(getAuditDiff(AuditEventCode.FileRenamed, "{")).toEqual({ type: "none" });
  });
});

describe("getAuditMetadataEntries", () => {
  it("hides ids and names, and labels the rest", () => {
    const entries = getAuditMetadataEntries(
      JSON.stringify({
        destination_id: "abc",
        destination_name: "Music",
        file_count: 3,
        file_ids: ["a", "b", "c"],
        hard_delete: false,
        name: "x",
      }),
    );

    expect(entries).toEqual([
      { label: "Destination", value: "Music" },
      { label: "Files", value: "3" },
      { label: "Deleted permanently", value: "No" },
    ]);
  });

  it("lists recorded changes as old to new", () => {
    const entries = getAuditMetadataEntries(
      JSON.stringify({
        changes: { title: { new: "B", old: "A" }, year: { new: "2024", old: null } },
      }),
    );

    expect(entries).toEqual([
      { label: "Title", value: "A → B" },
      { label: "Year", value: "none → 2024" },
    ]);
  });

  it("explains failed sign-in reasons", () => {
    expect(getAuditMetadataEntries('{"reason":"bad_password"}')).toEqual([
      { label: "Reason", value: "Wrong password" },
    ]);
  });
});
