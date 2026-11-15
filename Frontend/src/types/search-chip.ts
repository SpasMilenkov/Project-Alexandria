import type { SearchFilter, SearchFilterKey } from "@/features/explorer/search-filters";

// A filter as it shows in the search input: key and readable value, plus the
// filter itself so editing or removing the chip can hand it back.
export interface SearchChip {
  filter: SearchFilter;
  icon: string;
  id: string;
  key: SearchFilterKey;
  value: string;
}
