import type {
  WrappedCardFacts,
  WrappedCardResponse,
  WrappedDeckResponse,
  WrappedPeriodPoint,
  WrappedComparison,
} from "@/api/stats";

import { WrappedCardType as Type } from "@/enums/wrapped-card-type";
import {
  WrappedTimeScene as Scene,
  WrappedRhythmEvidence as Evidence,
  WrappedDurationTier as Tier,
  WrappedDurationCategory as Category,
  WrappedDurationRelationship as Relationship,
} from "@/enums/wrapped-story";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";

interface Profile {
  id: string;
  name: string;
  description: string;
  months: number[];
  hours: number[];
  share: number;
  palette: string;
  activeDays: number;
}

const profiles: Profile[] = [
  {
    id: "repeater",
    name: "The repeater",
    description: "120 hours, one dominant favorite, late nights.",
    months: [2, 4, 5, 8, 10, 20, 28, 18, 10, 7, 5, 3],
    hours: [9, 12, 18, 16, 8, 4, 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 2, 2, 3, 4, 5, 4, 2],
    share: 0.72,
    palette: "midnight-blue",
    activeDays: 140,
  },

  {
    id: "explorer",
    name: "The explorer",
    description: "The same 120 hours, spread over artists, months and hours.",
    months: [10, 11, 9, 12, 10, 8, 12, 11, 9, 10, 8, 10],
    hours: Array.from({ length: 24 }, () => 4),
    share: 0.12,
    palette: "daylight",
    activeDays: 210,
  },

  {
    id: "daily",
    name: "The daily regular",
    description: "A steady year with a strong morning rhythm.",
    months: [10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10],
    hours: [0, 0, 0, 0, 0, 1, 8, 12, 15, 13, 9, 7, 2, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0],
    share: 0.35,
    palette: "pale-gold",
    activeDays: 366,
  },

  {
    id: "weekend",
    name: "The weekend listener",
    description: "A concentrated evening habit and a long return.",
    months: [4, 7, 12, 8, 16, 20, 10, 8, 12, 4, 9, 10],
    hours: [2, 1, 0, 0, 0, 0, 0, 0, 1, 2, 1, 2, 4, 2, 3, 2, 3, 4, 12, 18, 20, 16, 8, 4],
    share: 0.4,
    palette: "ember",
    activeDays: 86,
  },

  {
    id: "seasonal",
    name: "The seasonal listener",
    description: "Real gaps and a dramatic summer chapter.",
    months: [0, 0, 0, 4, 6, 30, 36, 32, 8, 4, 0, 0],
    hours: [0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 4, 4, 10, 15, 20, 18, 12, 9, 7, 5, 3, 1, 0, 0],
    share: 0.27,
    palette: "daylight",
    activeDays: 100,
  },

  {
    id: "sparse",
    name: "The opening note",
    description: "A single track and 90 seconds. No invented personality.",
    months: [0, 0, 0, 0, 0, 0, 0, 0, 0.025, 0, 0, 0],
    hours: Array.from({ length: 24 }, (_, i) => (i === 12 ? 1 : 0)),
    share: 1,
    palette: "daylight",
    activeDays: 1,
  },

  {
    id: "missing",
    name: "Missing metadata",
    description: "Long filenames, no known artists, a complete recap.",
    months: [10, 8, 9, 12, 11, 10, 10, 10, 12, 8, 9, 11],
    hours: Array.from({ length: 24 }, () => 4),
    share: 0.2,
    palette: "midnight-blue",
    activeDays: 120,
  },

  {
    id: "balanced",
    name: "Around the clock",
    description: "No dominant time of day, so no forced persona.",
    months: [12, 8, 10, 8, 12, 10, 12, 8, 10, 8, 12, 10],
    hours: Array.from({ length: 24 }, () => 4),
    share: 0.25,
    palette: "pale-gold",
    activeDays: 220,
  },
];

profiles.push({
  ...profiles[2]!,
  id: "midday",
  name: "The midday listener",
  description: "A pronounced midday scene and 18 returns to one song.",
  hours: Array.from({ length: 24 }, (_, i) => (i >= 11 && i < 14 ? 20 : 1)),
});

const makeDeck = (profile: Profile): WrappedDeckResponse => {
  const sparse = profile.id === "sparse";
  const missing = profile.id === "missing";
  const year = profile.id === "daily" ? 2024 : 2025;
  const start = Date.UTC(year, 0, 1);
  const end = Date.UTC(year + 1, 0, 1);
  const dayCount = (end - start) / 86400000;

  const artists = [
    "Marble Cinema",
    "Soft Machines",
    "Late Botanist",
    "Paper Satellites",
    "Morning Assembly",
  ];

  const titles = [
    "A Place Between the Hours",
    "Everything in Its Own Time",
    "Letters from the Other Coast",
    "The Quiet Frequency",
    "Stay a Little Longer",
  ];

  const activeMonths = profile.months.filter((value) => value > 0).length;
  let remainingDays = profile.activeDays;
  let remainingMonths = activeMonths;
  const days: WrappedPeriodPoint[] = [];
  const months: WrappedPeriodPoint[] = [];

  profile.months.forEach((hours, month) => {
    const monthStart = Date.UTC(year, month, 1);
    const count = (Date.UTC(year, month + 1, 1) - monthStart) / 86400000;
    let active = 0;

    if (hours > 0) {
      active = Math.min(count, Math.ceil(remainingDays / remainingMonths));
      remainingDays -= active;
      remainingMonths--;
    }

    const total = Math.round(hours * 3600);

    const weights = Array.from({ length: active }, (_, index) => {
      if (profile.id === "daily") return 1;
      if (index === Math.floor(active / 2)) return 12;
      if (profile.id === "balanced" && index % 5 === 0) return 10;

      return 1;
    });

    const weightTotal = weights.reduce((sum, value) => sum + value, 0);
    let allocated = 0;

    const monthDays = Array.from({ length: count }, (_, index) => {
      let seconds = 0;

      if (index < active) {
        seconds = Math.floor((total * weights[index]!) / weightTotal);

        if (index === active - 1) seconds = total - allocated;

        allocated += seconds;
      }

      return {
        date: new Date(monthStart + index * 86400000).toISOString().slice(0, 10),
        seconds,
        plays: seconds > 0 ? Math.max(1, Math.round(seconds / 240)) : 0,
      };
    });

    days.push(...monthDays);

    months.push({
      date: new Date(monthStart).toISOString().slice(0, 10),
      seconds: total,
      plays: monthDays.reduce((sum, day) => sum + day.plays, 0),
      leader: artists[Math.floor(month / 3) % artists.length],
      leaderKey: `artist-${Math.floor(month / 3)}`,
    });
  });

  const active = days.filter((day) => day.seconds > 0);
  const first = active[0]!.date;
  const last = active[active.length - 1]!.date;
  const seconds = days.reduce((sum, day) => sum + day.seconds, 0);
  const sessions = days.reduce((sum, day) => sum + day.plays, 0);

  const make = (
    type: Type,
    headline: string,
    subline: string,
    fields: Partial<WrappedCardFacts> = {},
  ): WrappedCardResponse => ({
    id: String(type),
    type,
    headline,
    subline,
    entries: [],

    visual: {
      colorKey: profile.palette,
      saturation: "balanced",
      contrast: "crisp",
      density: 0,
      fillLevel: 0,
      shapeGrammar: "",
    },

    facts: {
      seconds: 0,
      count: 0,
      share: 0,
      baselineSeconds: 0,
      series: [],
      weights: [],
      ...fields,
    },
  });

  const comparison: WrappedComparison = {
    key: "five_earth_days",
    name: "Five Earth days",
    category: Category.Time,
    tier: Tier.OneTwentyPlus,
    durationMinutes: 7200,
    durationLabel: "120h",
    exact: true,
    relationship: Relationship.Approximately,
    copy: "About as much time as five Earth days.",
    catalogVersion: "2026-09-13.1",
  };

  const hero = make(
    Type.ListeningTime,
    sparse
      ? "Every soundtrack starts somewhere"
      : `${formatWrappedDuration(seconds)}. Entirely your sound.`,
    `${formatWrappedDuration(seconds)} of music · ${active.length} listening days`,
    {
      seconds,
      count: active.length,
      series: months,
      comparison: sparse ? null : comparison,

      detail:
        "Accumulated listening, not continuous playback. Development fixture; daily and monthly totals agree.",
    },
  );

  const topSongs = make(
    Type.TopSongs,
    "The tracks that defined it",
    "Ranked by listening time · shares of all your listening",
    { share: profile.share },
  );

  topSongs.entries = titles.slice(0, sparse ? 1 : 5).map((title, i) => {
    let share = profile.share;

    if (i > 0) share = Math.min(profile.share, (1 - profile.share) / 6);

    const plays = Math.max(1, Math.round(sessions * share));

    return {
      rank: i + 1,

      title: missing
        ? `session_recording_${year}_${title.replace(/ /g, "_")}_extended_final_master.flac`
        : title,

      artist: missing ? undefined : artists[i],
      seconds: Math.round(seconds * share),
      plays,
      share,
      subtitle: `${formatWrappedDuration(seconds * share)} · ${plays} plays`,
    };
  });

  const topArtists = make(Type.TopArtists, "What your year was built on", topSongs.subline!, {
    share: profile.share,
  });

  topArtists.entries = topSongs.entries.map((entry, i) => ({
    ...entry,
    title: artists[i]!,
    artist: undefined,
  }));

  const windows = [
    profile.hours.slice(0, 6),
    profile.hours.slice(6, 11),
    profile.hours.slice(11, 14),
    profile.hours.slice(14, 18),
    profile.hours.slice(18, 24),
  ].map((window) => window.reduce((sum, value) => sum + value, 0));

  const weightTotal = windows.reduce((sum, value) => sum + value, 0);

  const ranked = windows
    .map((value, scene) => ({ value, scene }))
    .sort((a, b) => b.value - a.value);

  const share = ranked[0]!.value / weightTotal;
  let evidence = Evidence.Balanced;
  let scene: Scene | null = null;

  if (sparse) evidence = Evidence.Sparse;
  else if (share >= 0.35 && (ranked[0]!.value - ranked[1]!.value) / weightTotal >= 0.1) {
    evidence = Evidence.Pronounced;
    scene = ranked[0]!.scene as Scene;
  }

  let headline = "Music on your own schedule";
  let subline = "Your listening is spread across the day.";

  if (sparse) {
    headline = "Your rhythm is taking shape";
    subline = "A few more listening days will reveal your pattern.";
  } else if (scene !== null) {
    headline = [
      "Late nights have your sound",
      "Early mornings have your sound",
      "Midday has your sound",
      "Afternoons have your sound",
      "Evenings have your sound",
    ][scene]!;

    subline = `${Math.round(share * 100)}% of your listening falls in this part of the day, measured in UTC.`;
  }

  const rhythm = make(Type.Persona, headline, subline, {
    weights: profile.hours,
    share,
    count: sessions,

    rhythm: {
      scene,
      evidence,
      share,
      windowSeconds: windows.map((value) => (value / weightTotal) * seconds),
    },

    detail: "Shares of listening in five named UTC windows, not per-hour intensity.",
  });

  const exploration = make(
    Type.Exploration,
    "New paths. Familiar places.",
    "New listening in color, familiar listening in the outer track.",
    { share: 1 - profile.share, count: 24 },
  );

  const chapters = make(
    Type.Chapters,
    "Your soundtrack had chapters",
    "Four artists took turns leading your months.",
    { series: months, count: 3 },
  );

  chapters.entries = months
    .filter((month, i) => month.seconds > 0 && i % 3 === 0)
    .map((month, i) => ({ rank: i + 1, title: month.leader!, subtitle: month.date.slice(0, 7) }));

  let run = 0;
  let longest = 0;
  let streakEnd = 0;

  days.forEach((day, index) => {
    run = day.seconds > 0 ? run + 1 : 0;

    if (run > longest) {
      longest = run;
      streakEnd = index;
    }
  });

  const streak = make(
    Type.Streak,
    "You kept the music going",
    `${longest} listening days in a row.`,
    {
      count: longest,
      from: days[streakEnd - longest + 1]!.date,
      to: days[streakEnd]!.date,
      series: days,
    },
  );

  const peak = [...active].sort((a, b) => b.seconds - a.seconds)[0]!;

  const busiest = make(
    Type.BusiestDay,
    `${peak.date} turned up the volume`,
    `${formatWrappedDuration(peak.seconds)} with your music.`,
    {
      seconds: peak.seconds,
      from: peak.date,
      series: days,
      detail: "Both halves show the same daily listening time. Choose a date to explore.",
    },
  );

  const returningDate = new Date(`${last}T00:00:00Z`).getTime();
  const gap = profile.id === "weekend" ? 620 : 62;

  const returning = make(
    Type.ReturningFavorite,
    `After ${gap} days, The Quiet Frequency came back`,
    "Then it stayed for another listening day.",
    {
      count: gap,
      from: new Date(returningDate - gap * 86400000).toISOString(),
      to: new Date(returningDate).toISOString(),
    },
  );

  const tally = profile.id === "repeater" ? 121 : 18;

  const replay = make(
    Type.MostReplayed,
    `Stay a Little Longer, ${tally} times`,
    "You kept coming back, across different listening days.",
    { count: tally },
  );

  replay.entries = [{ rank: 1, title: titles[4]!, artist: artists[4], plays: tally }];

  const bookends = make(
    Type.Bookends,
    sparse ? "The opening note" : "From the first note to the latest",
    "The edges of your soundtrack.",
    {
      count: active.length,
      from: first,
      to: last,
      series: days,
    },
  );

  bookends.entries = [{ rank: 1, title: titles[0]!, date: first, subtitle: "First recorded play" }];

  if (!sparse)
    bookends.entries.push({
      rank: 2,
      title: titles[4]!,
      date: last,
      subtitle: "Latest recorded play",
    });

  let cards = [
    hero,
    topArtists,
    topSongs,
    rhythm,
    exploration,
    chapters,
    streak,
    busiest,
    returning,
    replay,
    bookends,
  ];

  if (sparse) cards = [hero, topSongs, rhythm, bookends];
  if (missing) cards = cards.filter((card) => card.type !== Type.TopArtists);

  return {
    from: new Date(start).toISOString(),
    to: new Date(start + dayCount * 86400000).toISOString(),
    visualIdentity: profile.id,
    schemaVersion: 3,
    recipeVersion: 2,
    deck: { cards },

    summary: {
      seconds,
      sessions,
      qualifiedPlayCount: sessions,
      tracks: sparse ? 1 : 86,
      artists: missing ? 0 : 24,
      activeDays: active.length,
      knownArtistShare: missing ? 0 : 1,
      hasPriorHistory: !sparse,
    },
  };
};

export const wrappedFixtures = profiles.map((profile) => ({
  ...profile,
  response: makeDeck(profile),
}));
