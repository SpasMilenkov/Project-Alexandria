interface WrappedYearRange {
  from: string;
  to: string;
}

export const MIN_WRAPPED_YEAR = 2020;

export const parseWrappedYear = (value: unknown, fallbackYear: number): number => {
  if (typeof value === "string") {
    const parsed = Number(value);

    if (
      /^\d{4}$/u.test(value) &&
      Number.isInteger(parsed) &&
      parsed >= MIN_WRAPPED_YEAR &&
      parsed <= fallbackYear
    ) {
      return parsed;
    }

    return fallbackYear;
  }

  return fallbackYear;
};

// Half-open UTC interval includes the last fractional second of a past year.
export const yearRange = (year: number, now: Date = new Date()): WrappedYearRange => {
  const from = new Date(Date.UTC(year, 0, 1, 0, 0, 0)).toISOString();
  const endOfYear = new Date(Date.UTC(year + 1, 0, 1));
  const to = (year >= now.getUTCFullYear() ? now : endOfYear).toISOString();

  return { from, to };
};

export const padRank = (rank: number): string => String(rank).padStart(2, "0");

export const wrappedEmptyState = (year: number, currentYear: number) => {
  if (year < currentYear) {
    return {
      title: `No recorded listening in ${year}`,
      description: `There are no recorded Alexandria listening sessions for this year. Music you play now contributes to your ${currentYear} Wrapped, not ${year}.`,
      actionLabel: `View ${currentYear} Wrapped`,
    };
  }

  return {
    title: "Nothing to recap yet",
    description: `Play some music and your ${year} story will start taking shape.`,
    actionLabel: "Find your soundtrack",
  };
};
