import { type CalendarDate, getLocalTimeZone } from "@internationalized/date";
import { useDebounceFn } from "@vueuse/core";
import { computed, reactive, ref } from "vue";
import { z } from "zod";

import type { DateRangeValue } from "@/components/common/DateRangeFilter.vue";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { UserRole } from "@/enums/UserRole";
import { userQueryApiSchema, userQueryUiSchema } from "@/schemas/user";
import { formatDayMonth, formatDayMonthRange } from "@/utils/date-formatters";

type DateFilterKey = "created" | "updated" | "deleted" | "lockedOut";

type DateField =
  | "createdAfter"
  | "createdBefore"
  | "updatedAfter"
  | "updatedBefore"
  | "deletedAfter"
  | "deletedBefore"
  | "lockedOutAfter"
  | "lockedOutBefore";

export interface DateFilter {
  key: DateFilterKey;
  label: string;
  icon: string;
  after: DateField;
  before: DateField;
}

export type AccountsMode = "existing" | "all" | "deleted";

export type LockMode = "any" | "unlocked" | "locked";

export interface FilterPill {
  key: string;
  label: string;
  clear: () => void;
}

export const ANY_ROLE = "any";

export const DATE_FILTERS: DateFilter[] = [
  {
    after: "createdAfter",
    before: "createdBefore",
    icon: "i-lucide-calendar-plus",
    key: "created",
    label: "Created",
  },
  {
    after: "updatedAfter",
    before: "updatedBefore",
    icon: "i-lucide-calendar-clock",
    key: "updated",
    label: "Updated",
  },
  {
    after: "deletedAfter",
    before: "deletedBefore",
    icon: "i-lucide-calendar-x",
    key: "deleted",
    label: "Deleted",
  },
  {
    after: "lockedOutAfter",
    before: "lockedOutBefore",
    icon: "i-lucide-lock",
    key: "lockedOut",
    label: "Locked",
  },
];

const roleLabels: Record<UserRole, string> = {
  [UserRole.Admin]: "Administrators",
  [UserRole.User]: "Users",
};

const accountLabels: Record<AccountsMode, string> = {
  all: "Including deleted",
  deleted: "Deleted only",
  existing: "Existing",
};

const lockLabels: Record<LockMode, string> = {
  any: "Any",
  locked: "Locked",
  unlocked: "Unlocked",
};

const lockLookup: Record<LockMode, boolean | undefined> = {
  any: undefined,
  locked: true,
  unlocked: false,
};

export const roleOptions = [
  { label: "Any role", value: ANY_ROLE },
  { label: "User", value: UserRole.User },
  { label: "Administrator", value: UserRole.Admin },
];

export const accountOptions: { label: string; value: AccountsMode }[] = [
  { label: "Existing", value: "existing" },
  { label: "All", value: "all" },
  { label: "Deleted", value: "deleted" },
];

export const lockOptions: { label: string; value: LockMode }[] = [
  { label: "Any", value: "any" },
  { label: "Unlocked", value: "unlocked" },
  { label: "Locked", value: "locked" },
];

export const sortByOptions = [
  { label: "Name", value: SortBy.Name },
  { label: "Created", value: SortBy.CreatedAt },
  { label: "Last updated", value: SortBy.UpdatedAt },
  { label: "File count", value: SortBy.FileCount },
  { label: "Storage used", value: SortBy.TotalSize },
];

export const pageSizeOptions = [
  { label: "10 per page", value: 10 },
  { label: "20 per page", value: 20 },
  { label: "50 per page", value: 50 },
  { label: "100 per page", value: 100 },
];

const formatRangePill = (start: CalendarDate, end: CalendarDate) => {
  const startDate = start.toDate(getLocalTimeZone());

  if (start.compare(end) === 0) {
    return formatDayMonth(startDate);
  }

  return formatDayMonthRange(startDate, end.toDate(getLocalTimeZone()));
};

type CountableState = { userEmail?: string; role?: UserRole } & Partial<Record<DateField, unknown>>;

const countAdvancedFilters = (state: CountableState) => {
  let count = 0;
  if (state.userEmail) {
    count++;
  }
  if (state.role !== undefined) {
    count++;
  }
  for (const filter of DATE_FILTERS) {
    if (state[filter.after] && state[filter.before]) {
      count++;
    }
  }
  return count;
};

const dateFilterPills = (
  getRange: (filter: DateFilter) => DateRangeValue,
  setRange: (filter: DateFilter, range: DateRangeValue) => void,
): FilterPill[] =>
  DATE_FILTERS.flatMap((filter) => {
    const { start, end } = getRange(filter);
    if (!start || !end) {
      return [];
    }
    return [
      {
        clear: () => setRange(filter, {}),
        key: filter.key,
        label: `${filter.label} ${formatRangePill(start, end)}`,
      },
    ];
  });

export const useUserRegistryFilters = () => {
  const uiState = reactive(userQueryUiSchema.parse({}));
  const apiState = ref(userQueryApiSchema.parse(uiState));
  const emailDraft = ref("");
  const emailError = ref<string>();

  const commitQuery = () => {
    const parsed = userQueryApiSchema.safeParse(uiState);

    // Inputs are constrained so this only trips on a bug; keep the last good query.
    if (!parsed.success) {
      return;
    }

    apiState.value = parsed.data;
  };

  const applyFilters = () => {
    uiState.page = 0;
    commitQuery();
  };

  const debouncedCommit = useDebounceFn(applyFilters, 350);

  const commitEmail = () => {
    const value = emailDraft.value.trim();

    if (!value) {
      emailError.value = undefined;
      uiState.userEmail = undefined;
      applyFilters();
      return;
    }

    if (!z.email().safeParse(value).success) {
      emailError.value = "Enter a full email address";
      return;
    }

    emailError.value = undefined;
    uiState.userEmail = value;
    applyFilters();
  };

  const debouncedEmailCommit = useDebounceFn(commitEmail, 500);

  // Reactive unwrapping strips CalendarDate's private brand from the type only.
  const getRange = (filter: DateFilter): DateRangeValue => ({
    end: uiState[filter.before] as CalendarDate | undefined,
    start: uiState[filter.after] as CalendarDate | undefined,
  });

  const setRange = (filter: DateFilter, range: DateRangeValue) => {
    uiState[filter.after] = range.start;
    uiState[filter.before] = range.end;
    applyFilters();
  };

  const accountsMode = computed<AccountsMode>({
    get: () => {
      if (uiState.showDeletedOnly) {
        return "deleted";
      }
      if (uiState.showDeleted) {
        return "all";
      }
      return "existing";
    },
    set: (mode) => {
      uiState.showDeleted = mode === "all";
      uiState.showDeletedOnly = mode === "deleted";

      // A deletion date can never match while deleted accounts are hidden.
      if (mode === "existing") {
        uiState.deletedAfter = undefined;
        uiState.deletedBefore = undefined;
      }

      applyFilters();
    },
  });

  const lockMode = computed<LockMode>({
    get: () => {
      if (uiState.isLockedOut === true) {
        return "locked";
      }
      if (uiState.isLockedOut === false) {
        return "unlocked";
      }
      return "any";
    },
    set: (mode) => {
      uiState.isLockedOut = lockLookup[mode];
      applyFilters();
    },
  });

  const roleFilter = computed<UserRole | typeof ANY_ROLE>({
    get: () => uiState.role ?? ANY_ROLE,
    set: (value) => {
      if (value === ANY_ROLE) {
        uiState.role = undefined;
      } else {
        uiState.role = value;
      }
      applyFilters();
    },
  });

  const sortBy = computed({
    get: () => uiState.sortBy,
    set: (value: SortBy) => {
      uiState.sortBy = value;
      applyFilters();
    },
  });

  const pageSize = computed({
    get: () => uiState.pageSize,
    set: (value: number) => {
      uiState.pageSize = value;
      applyFilters();
    },
  });

  const isAscending = computed(() => uiState.sortDirection === SortDirection.Asc);

  const toggleSortDirection = () => {
    if (isAscending.value) {
      uiState.sortDirection = SortDirection.Desc;
    } else {
      uiState.sortDirection = SortDirection.Asc;
    }
    applyFilters();
  };

  const isDateFilterDisabled = (filter: DateFilter) =>
    filter.key === "deleted" && accountsMode.value === "existing";

  const clearFilters = () => {
    const defaults = userQueryUiSchema.parse({});

    // Sorting and page size are view preferences, not filters, so they survive a reset.
    Object.assign(uiState, {
      ...defaults,
      pageSize: uiState.pageSize,
      sortBy: uiState.sortBy,
      sortDirection: uiState.sortDirection,
    });

    emailDraft.value = "";
    emailError.value = undefined;
    commitQuery();
  };

  const activeFilterPills = computed(() => {
    const pills: FilterPill[] = [];

    if (uiState.userName) {
      pills.push({
        clear: () => {
          uiState.userName = undefined;
          applyFilters();
        },
        key: "userName",
        label: `“${uiState.userName}”`,
      });
    }

    if (uiState.userEmail) {
      pills.push({
        clear: () => {
          emailDraft.value = "";
          commitEmail();
        },
        key: "userEmail",
        label: uiState.userEmail,
      });
    }

    if (uiState.role !== undefined) {
      pills.push({
        clear: () => (roleFilter.value = ANY_ROLE),
        key: "role",
        label: roleLabels[uiState.role],
      });
    }

    if (accountsMode.value !== "existing") {
      pills.push({
        clear: () => (accountsMode.value = "existing"),
        key: "accounts",
        label: accountLabels[accountsMode.value],
      });
    }

    if (lockMode.value !== "any") {
      pills.push({
        clear: () => (lockMode.value = "any"),
        key: "lock",
        label: lockLabels[lockMode.value],
      });
    }

    return [...pills, ...dateFilterPills(getRange, setRange)];
  });

  const advancedFilterCount = computed(() => countAdvancedFilters(uiState));

  return {
    accountsMode,
    activeFilterPills,
    advancedFilterCount,
    apiState,
    clearFilters,
    commitQuery,
    debouncedCommit,
    debouncedEmailCommit,
    emailDraft,
    emailError,
    getRange,
    isAscending,
    isDateFilterDisabled,
    lockMode,
    pageSize,
    roleFilter,
    setRange,
    sortBy,
    toggleSortDirection,
    uiState,
  };
};
