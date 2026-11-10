const NUMBER = new Intl.NumberFormat();

/** "1,234", grouped the way the viewer's locale writes numbers. */
export const formatNumber = (value: number): string => NUMBER.format(value);

/** The noun alone, for templates that style the number apart from it. */
export const pluralize = (count: number, singular: string, plural = `${singular}s`): string =>
  count === 1 ? singular : plural;

/** "1 file" or "1,234 files"; pass the plural for irregular nouns ("entry", "entries"). */
export const formatCount = (count: number, singular: string, plural = `${singular}s`): string =>
  `${formatNumber(count)} ${pluralize(count, singular, plural)}`;
