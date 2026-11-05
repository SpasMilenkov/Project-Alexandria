import { hashString } from "@/utils/random";

export interface GeneratedCover {
  background: string;
  base: string;
}

// Dark base plus a bright mark; every pair keeps the mark visible in both themes.
const COVER_PALETTE: readonly (readonly [string, string])[] = [
  ["#1e3a5f", "#f4a261"],
  ["#5b2a86", "#f72585"],
  ["#264653", "#e9c46a"],
  ["#3d0c11", "#ff6b35"],
  ["#0b525b", "#90e0ef"],
  ["#2b2d42", "#ef233c"],
  ["#4a3f8c", "#ffca3a"],
  ["#14213d", "#fca311"],
  ["#3a5a40", "#dad7cd"],
  ["#5e3d26", "#ede0d4"],
  ["#03045e", "#00b4d8"],
  ["#540b0e", "#e09f3e"],
  ["#1b4332", "#95d5b2"],
  ["#432818", "#ffb703"],
  ["#22223b", "#c9ada7"],
  ["#370617", "#f48c06"],
  ["#012a4a", "#a9d6e5"],
  ["#3c1642", "#f0a6ca"],
  ["#283618", "#dda15e"],
  ["#0d1b2a", "#e0e1dd"],
];

const SPOTS: readonly (readonly [string, number])[] = [
  ["72% 28%", 26],
  ["30% 68%", 32],
  ["50% 46%", 22],
  ["78% 76%", 38],
  ["24% 24%", 20],
];

// Stable per seed, so a placeholder never changes between visits.
export const generatedCover = (seed: string): GeneratedCover => {
  const hash = hashString(seed || "alexandria");
  const [base, mark] = COVER_PALETTE[hash % COVER_PALETTE.length]!;
  const [position, size] = SPOTS[(hash >>> 8) % SPOTS.length]!;

  const background = [
    `radial-gradient(circle at ${position}, ${mark} 0 ${size}%, transparent ${size + 0.6}%)`,
    `radial-gradient(circle at 22% 82%, transparent 0 13%, ${mark}99 13.5% 15.5%, transparent 16%)`,
    base,
  ].join(", ");

  return { background, base };
};

interface CoverSeedSource {
  fileId: string;
  artist?: string | null;
  album?: string | null;
}

// Tracks from one album share a placeholder, which reads as intentional art.
export const coverSeed = ({ fileId, artist, album }: CoverSeedSource): string => {
  const albumName = album?.trim();

  if (albumName) return `${artist?.trim() ?? ""}|${albumName}`;

  return fileId;
};
