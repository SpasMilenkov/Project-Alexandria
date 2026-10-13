import type { HomeFocusOptions } from "@/types/home";

export type HomeFocusPlan = Pick<
  HomeFocusOptions,
  "focusMinutes" | "shortBreakMinutes" | "longBreakMinutes" | "focusSessions"
>;
export type HomeFocusPhase = "focus" | "shortBreak" | "longBreak";

export interface HomeFocusState {
  phase: HomeFocusPhase;
  status: "idle" | "running" | "paused" | "complete";
  session: number;
  remaining: number;
  endsAt: number | null;
  plan: HomeFocusPlan;
}

export const homeFocusPlan = (options: HomeFocusPlan): HomeFocusPlan => ({
  focusMinutes: options.focusMinutes,
  shortBreakMinutes: options.shortBreakMinutes,
  longBreakMinutes: options.longBreakMinutes,
  focusSessions: options.focusSessions,
});

export const homeFocusDuration = (phase: HomeFocusPhase, plan: HomeFocusPlan) => {
  if (phase === "shortBreak") return plan.shortBreakMinutes * 60;
  if (phase === "longBreak") return plan.longBreakMinutes * 60;

  return plan.focusMinutes * 60;
};

export const homeFocusIdle = (plan: HomeFocusPlan): HomeFocusState => ({
  phase: "focus",
  status: "idle",
  session: 1,
  remaining: plan.focusMinutes * 60,
  endsAt: null,
  plan: homeFocusPlan(plan),
});

export const homeFocusRemaining = (state: HomeFocusState, now: number) => {
  if (state.status === "running" && state.endsAt !== null)
    return Math.min(state.remaining, Math.max(0, Math.ceil((state.endsAt - now) / 1000)));

  return state.remaining;
};

export const homeFocusNext = (state: HomeFocusState): HomeFocusState => {
  let phase: HomeFocusPhase = "focus";
  let session = state.session;

  if (state.phase === "focus")
    phase = session === state.plan.focusSessions ? "longBreak" : "shortBreak";
  else session = session === state.plan.focusSessions ? 1 : session + 1;

  return {
    ...state,
    phase,
    session,
    status: "idle",
    remaining: homeFocusDuration(phase, state.plan),
    endsAt: null,
  };
};

export const homeFocusToggle = (state: HomeFocusState, now: number): HomeFocusState => {
  const remaining = homeFocusRemaining(state, now);

  if (state.status === "running") {
    if (remaining === 0) return { ...state, status: "complete", remaining: 0, endsAt: null };

    return { ...state, status: "paused", remaining, endsAt: null };
  }

  const next = state.status === "complete" ? homeFocusNext(state) : state;

  return { ...next, status: "running", endsAt: now + next.remaining * 1000 };
};

export const homeFocusLabel = (state: HomeFocusState) => {
  if (state.status === "complete")
    return state.phase === "focus" ? "Session complete" : "Break complete";
  if (state.status === "paused") return "Paused";
  if (state.status === "idle") return "Ready to focus";
  if (state.phase === "longBreak") return "Long break";
  if (state.phase === "shortBreak") return "Break";

  return "Focus";
};

export const homeFocusAction = (state: HomeFocusState) => {
  if (state.status === "running") return "Pause";
  if (state.status === "paused") return "Resume";
  if (state.status === "complete") return state.phase === "focus" ? "Start break" : "Start next";

  return "Start";
};

export const parseHomeFocusState = (value: unknown): HomeFocusState | null => {
  if (!value || typeof value !== "object") return null;

  const state = value as Partial<HomeFocusState>;
  const plan = state.plan;
  if (!plan || typeof plan !== "object") return null;

  const bounds: [unknown, number][] = [
    [plan.focusMinutes, 180],
    [plan.shortBreakMinutes, 60],
    [plan.longBreakMinutes, 120],
    [plan.focusSessions, 8],
  ];
  if (
    bounds.some(
      ([number, max]) =>
        typeof number !== "number" || !Number.isInteger(number) || number < 1 || number > max,
    )
  )
    return null;
  if (!state.phase || !["focus", "shortBreak", "longBreak"].includes(state.phase)) return null;
  if (!state.status || !["idle", "running", "paused", "complete"].includes(state.status))
    return null;
  if (!Number.isInteger(state.session) || state.session! < 1 || state.session! > plan.focusSessions)
    return null;
  if (
    typeof state.remaining !== "number" ||
    !Number.isInteger(state.remaining) ||
    state.remaining < 0 ||
    state.remaining > homeFocusDuration(state.phase, plan)
  )
    return null;
  if (
    state.status === "running" &&
    (typeof state.endsAt !== "number" || !Number.isFinite(state.endsAt))
  )
    return null;
  if (state.status !== "running" && state.endsAt !== null) return null;

  return state as HomeFocusState;
};
