// One row in the search suggestion list: a filter key to start, or a value to pick.
export interface SearchSuggestion {
  apply: () => void;
  hint?: string;
  icon: string;
  id: string;
  label: string;
}
