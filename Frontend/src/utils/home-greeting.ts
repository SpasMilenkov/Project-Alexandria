import type { HomeGreetingOptions } from "@/types/home";

import {
  HomeGreetingColorMode,
  HomeGreetingDecoration,
  HomeGreetingStyle,
} from "@/enums/home-widget";
import { homeClockGreeting, homeClockNumbers } from "@/utils/home-clock";

export const HOME_GREETING_STYLES = [
  {
    value: HomeGreetingStyle.Minimal,
    label: "Minimal",
    description: "A quiet greeting with a small decoration beside it.",
  },
  {
    value: HomeGreetingStyle.Welcome,
    label: "Welcome",
    description: "A generous headline and room for a little personality.",
  },
];

export const HOME_GREETING_DECORATIONS = [
  { value: HomeGreetingDecoration.None, label: "None" },
  { value: HomeGreetingDecoration.SunPixel, label: "Pixel sun" },
  { value: HomeGreetingDecoration.MoonPixel, label: "Pixel moon" },
  { value: HomeGreetingDecoration.CoffeePixel, label: "Pixel coffee" },
  { value: HomeGreetingDecoration.PlantPixel, label: "Pixel plant" },
  { value: HomeGreetingDecoration.CatPixel, label: "Pixel cat" },
  { value: HomeGreetingDecoration.DogPixel, label: "Pixel dog" },
  { value: HomeGreetingDecoration.DuckPixel, label: "Pixel duck" },
  { value: HomeGreetingDecoration.VinylPixel, label: "Pixel vinyl" },
  { value: HomeGreetingDecoration.WaveEmoji, label: "👋 Waving hand" },
  { value: HomeGreetingDecoration.CoffeeEmoji, label: "☕ Coffee" },
  { value: HomeGreetingDecoration.BooksEmoji, label: "📚 Books" },
  { value: HomeGreetingDecoration.SunEmoji, label: "☀️ Sun" },
  { value: HomeGreetingDecoration.MoonEmoji, label: "🌙 Moon" },
  { value: HomeGreetingDecoration.PlantEmoji, label: "🪴 Plant" },
  { value: HomeGreetingDecoration.CatEmoji, label: "🐈 Cat" },
  { value: HomeGreetingDecoration.DogEmoji, label: "🐕 Dog" },
  { value: HomeGreetingDecoration.DuckEmoji, label: "🦆 Duck" },
  { value: HomeGreetingDecoration.VinylEmoji, label: "💿 Record" },
];

export const HOME_GREETING_COLOR_MODES = [
  { value: HomeGreetingColorMode.Monochrome, label: "Monochrome" },
  { value: HomeGreetingColorMode.Colored, label: "Colored" },
];

export const isHomeGreetingPixelArt = (decoration: HomeGreetingDecoration) =>
  decoration >= HomeGreetingDecoration.SunPixel && decoration <= HomeGreetingDecoration.VinylPixel;

export const HOME_GREETING_EMOJIS: Partial<Record<HomeGreetingDecoration, string>> = {
  [HomeGreetingDecoration.WaveEmoji]: "👋",
  [HomeGreetingDecoration.CoffeeEmoji]: "☕",
  [HomeGreetingDecoration.BooksEmoji]: "📚",
  [HomeGreetingDecoration.SunEmoji]: "☀️",
  [HomeGreetingDecoration.MoonEmoji]: "🌙",
  [HomeGreetingDecoration.PlantEmoji]: "🪴",
  [HomeGreetingDecoration.CatEmoji]: "🐈",
  [HomeGreetingDecoration.DogEmoji]: "🐕",
  [HomeGreetingDecoration.DuckEmoji]: "🦆",
  [HomeGreetingDecoration.VinylEmoji]: "💿",
};

export const homeGreetingHeading = (
  timestamp: number,
  options: HomeGreetingOptions,
  accountName: string | null | undefined,
) => {
  const heading =
    options.greetingHeading?.trim() ||
    homeClockGreeting(homeClockNumbers(timestamp, undefined).hour24);
  const name = accountName?.trim();

  if (options.greetingShowName && name) return `${heading}, ${name}`;

  return heading;
};
