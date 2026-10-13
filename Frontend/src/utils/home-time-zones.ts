interface HomeTimeZoneChoice {
  label: string;
  value: string;
  disabled?: boolean;
}

export const homeTimeZoneLabel = (identifier: unknown): string => {
  if (identifier === "local") {
    try {
      const local = new Intl.DateTimeFormat().resolvedOptions().timeZone;

      if (local) return homeTimeZoneLabel(local);
    } catch {
      return "Local time";
    }

    return "Local time";
  }
  if (typeof identifier !== "string" || !identifier) return "Saved time zone";

  return identifier.split("_").join(" ").split("/").join(" / ");
};

export const resolveHomeTimeZone = (identifier: unknown) => {
  if (identifier === "local") return { timeZone: undefined, unavailable: false };
  if (typeof identifier !== "string" || !identifier)
    return { timeZone: undefined, unavailable: true };

  try {
    const formatter = new Intl.DateTimeFormat(undefined, { timeZone: identifier });

    return { timeZone: formatter.resolvedOptions().timeZone, unavailable: false };
  } catch {
    return { timeZone: undefined, unavailable: true };
  }
};

export const homeTimeZoneCatalogue = (identifiers: string[]): HomeTimeZoneChoice[] =>
  [...new Set(identifiers)]
    .filter((identifier) => !resolveHomeTimeZone(identifier).unavailable)
    .map((identifier) => {
      let label = homeTimeZoneLabel(identifier);

      if (identifier === "local") label = `Local (${label})`;

      return { label, value: identifier };
    });

export const homeTimeZoneChoices = (
  catalogue: HomeTimeZoneChoice[],
  saved: string,
): HomeTimeZoneChoice[] => {
  const choices = [...catalogue];

  if (!choices.some((choice) => choice.value === saved)) {
    choices.unshift({ label: `${homeTimeZoneLabel(saved)} (saved)`, value: saved, disabled: true });
  }

  return choices;
};
