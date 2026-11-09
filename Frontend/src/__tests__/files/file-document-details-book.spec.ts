import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { DocumentMetadataDto } from "@/api/file";

import FileDocumentDetails from "@/components/dashboard/file-system/FileDocumentDetails.vue";
import { isEbookMimeType } from "@/utils/files/mimetype";

const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
}));

const book = (overrides: Partial<DocumentMetadataDto> = {}): DocumentMetadataDto => ({
  application: null,
  author: "Isaac Asimov",
  authoredAt: "2001-03-04T00:00:00Z",
  authoredOffsetMinutes: null,
  company: null,
  editingMinutes: null,
  fileVersionId: "v1",
  hasPersonalDetails: false,
  isDrmProtected: false,
  isExtracted: true,
  isPasswordProtected: false,
  isbn: "9780804429573",
  keywords: "Science fiction",
  language: "en",
  lastModifiedBy: null,
  modifiedAt: "2024-05-02T00:00:00Z",
  modifiedOffsetMinutes: null,
  pageCount: null,
  paperSize: null,
  producer: null,
  publisher: "Gnome Press",
  series: "Foundation",
  seriesIndex: 2,
  sheetCount: null,
  slideCount: null,
  subject: null,
  title: "Foundation and Empire",
  wordCount: 121071,
  ...overrides,
});

const mountDetails = (data: DocumentMetadataDto | undefined, isBook: boolean) => {
  mockUseQuery.mockReturnValue({
    data: ref(data),
    error: ref<Error | null>(null),
    isPending: ref(false),
  });

  return mount(FileDocumentDetails, {
    props: { enabled: true, fileId: "f1", isBook, versionId: "v1" },
  });
};

describe("FileDocumentDetails for books", () => {
  beforeEach(() => {
    mockUseQuery.mockReset();
  });

  it("speaks of publishing instead of writing", () => {
    const text = mountDetails(book(), true).text();

    expect(text).toContain("Book details");
    expect(text).toContain("Published");
    expect(text).toContain("Last updated");
    expect(text).not.toContain("Written");
  });

  it("shows the series position, publisher and ISBN", () => {
    const text = mountDetails(book(), true).text();

    expect(text).toContain("Foundation");
    expect(text).toContain("Book 2");
    expect(text).toContain("Gnome Press");
    expect(text).toContain("9780804429573");
    expect(text).toContain("Subjects");
  });

  it("flags copy protection", () => {
    const text = mountDetails(book({ isDrmProtected: true }), true).text();

    expect(text).toContain("Copy protected");
  });

  it("keeps the document wording for office files", () => {
    const text = mountDetails(book({ isbn: null, publisher: null, series: null }), false).text();

    expect(text).toContain("Document details");
    expect(text).toContain("Written");
    expect(text).toContain("Keywords");
  });

  it("explains that unread books are picked up in the background", () => {
    const text = mountDetails(book({ isExtracted: false }), true).text();

    expect(text).toContain("within a few minutes");
  });
});

describe("isEbookMimeType", () => {
  it("matches EPUB and Kindle formats regardless of case or padding", () => {
    expect(isEbookMimeType("application/epub+zip")).toBe(true);
    expect(isEbookMimeType(" Application/EPUB+zip ")).toBe(true);
    expect(isEbookMimeType("application/vnd.amazon.mobi8-ebook")).toBe(true);
    expect(isEbookMimeType("application/x-mobipocket-ebook")).toBe(true);
    expect(isEbookMimeType("application/pdf")).toBe(false);
    expect(isEbookMimeType(null)).toBe(false);
  });
});
