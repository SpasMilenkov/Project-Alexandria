import { PiniaColada, useQueryCache } from "@pinia/colada";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, ref } from "vue";

import type { PaginatedResponse } from "@/api/directory";

import {
  AuditEventCode,
  type AuditLogResult,
  EntityType,
  LogSource,
  OperationType,
  activityApi,
} from "@/api/activity";
import { publicStatusApi } from "@/api/publicStatus";
import HomeRecentActivityWidgetOptions from "@/components/dashboard/home/HomeRecentActivityWidgetOptions.vue";
import HomeSystemStatusWidgetOptions from "@/components/dashboard/home/HomeSystemStatusWidgetOptions.vue";
import HomeRecentActivityWidget from "@/components/dashboard/home/widgets/HomeRecentActivityWidget.vue";
import HomeSystemStatusWidget from "@/components/dashboard/home/widgets/HomeSystemStatusWidget.vue";
import { ServiceType } from "@/enums";
import { HomeActivityStyle, HomeStatusStyle, HomeWidgetType } from "@/enums/home-widget";
import { SortDirection } from "@/enums/SortDirection";
import { homeRecentActivity, homeSystemStatus } from "@/queries/home-status-activity";
import { useHomeStore } from "@/stores/home";
import { createHomeWidget, defaultHomeLayout } from "@/utils/home-layout";
import {
  homeActivityDescription,
  homeActivityDisplayRows,
  homeActivityGroups,
  homeActivityTime,
  homeCurrentStatusRows,
  homeHistoryRows,
  homeOverallStatus,
} from "@/utils/home-status-activity";

vi.mock("@/composables/useHomeWidgetRows", () => ({ useHomeWidgetRows: () => ref(100) }));

const event = (patch: Partial<AuditLogResult> = {}): AuditLogResult => ({
  entityId: "file",
  entityType: EntityType.File,
  eventCode: AuditEventCode.FileCreated,
  operationType: OperationType.Create,
  logSource: LogSource.Trigger,
  timestamp: "2026-10-08T12:00:00Z",
  userId: "alice",
  ...patch,
});

const page = (items: AuditLogResult[]): PaginatedResponse<AuditLogResult> => ({
  items,
  currentPage: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
  hasNext: false,
  hasPrevious: false,
});

const stubs = {
  UIcon: true,
  UBadge: { template: "<span><slot /></span>" },
  UButton: { template: "<button><slot /></button>" },
  UFormField: { template: "<div><slot /></div>" },
  RouterLink: { props: ["to"], template: '<a :href="to"><slot /></a>' },
};

const mountWidget = (type: HomeWidgetType.SystemStatus | HomeWidgetType.RecentActivity) => {
  const pinia = createPinia();
  const store = useHomeStore(pinia);
  store.setOwner("alice");
  store.hydrate("alice", {
    initialized: true,
    showWidgetTitles: false,
    showHeaderGreeting: true,
    useSeparateMobileLayout: false,
    desktop: defaultHomeLayout(),
    mobile: null,
  });

  const component =
    type === HomeWidgetType.SystemStatus ? HomeSystemStatusWidget : HomeRecentActivityWidget;
  const widget = createHomeWidget(type, "widget");
  const wrapper = mount(component, {
    props: { widget },
    global: { plugins: [pinia, PiniaColada], stubs },
  });

  return { wrapper, store, widget };
};

afterEach(() => vi.restoreAllMocks());

describe("Home public status models", () => {
  it("uses the seven real services and never assumes absent states are healthy", () => {
    const rows = homeCurrentStatusRows([{ serviceType: ServiceType.Api, status: "Healthy" }]);

    expect(rows).toHaveLength(7);
    expect(rows.map((row) => row.label)).not.toContain("Database");
    expect(rows.map((row) => row.label)).not.toContain("Storage");
    expect(rows[0]!.state).toBe("Healthy");
    expect(rows.slice(1).every((row) => row.state === "Unknown")).toBe(true);
    expect(homeOverallStatus(rows.map((row) => row.state))).toBe("Unknown");
    expect(homeOverallStatus([])).toBe("Unknown");
    expect(homeOverallStatus(["Healthy", "Down", "Degraded", "Unknown"])).toBe("Down");
    expect(homeOverallStatus(["Healthy", "Degraded", "Unknown"])).toBe("Degraded");
  });

  it("preserves today's recorded incident and builds exactly thirty UTC dates across years", () => {
    const rows = homeHistoryRows(
      [
        {
          serviceType: ServiceType.Api,
          days: [
            { date: "2027-01-01", status: "Down" },
            { date: "2026-12-31", status: "Healthy" },
          ],
        },
      ],
      new Date("2027-01-01T23:30:00Z"),
    );
    const api = rows[0]!;

    expect(api.days).toHaveLength(30);
    expect(api.days[0]).toEqual({ date: "2026-12-03", state: "Unknown" });
    expect(api.days.at(-2)).toEqual({ date: "2026-12-31", state: "Healthy" });
    expect(api.days.at(-1)).toEqual({ date: "2027-01-01", state: "Down" });
    expect(rows[1]!.days.every((day) => day.state === "Unknown")).toBe(true);
  });
});

describe("Home recent activity formatting", () => {
  it("adds safe names to common events and uses new names for renames", () => {
    expect(homeActivityDescription(event({ metadata: '{"name":" report.pdf "}' }))).toBe(
      "File created · report.pdf",
    );
    expect(
      homeActivityDescription(
        event({
          eventCode: AuditEventCode.DirectoryRenamed,
          metadata: '{"old":"Old","new":"Music"}',
        }),
      ),
    ).toBe("Folder renamed · Music");
    expect(
      homeActivityDescription(
        event({ eventCode: AuditEventCode.FileMoved, metadata: '{"new_directory_id":"uuid"}' }),
      ),
    ).toBe("File moved");
  });

  it.each([undefined, "{", "null", "[]", '{"name":{"unsafe":"object"}}', '{"name":"  "}'])(
    "falls back to existing labels for malformed or missing names (%s)",
    (metadata) => {
      expect(homeActivityDescription(event({ metadata }))).toBe("File created");
    },
  );

  it("does not substitute unsupported device claims or expose account metadata", () => {
    expect(
      homeActivityDescription(
        event({ eventCode: AuditEventCode.UserLogin, metadata: '{"name":"new device"}' }),
      ),
    ).toBe("Signed in");
    expect(
      homeActivityDescription(
        event({
          eventCode: AuditEventCode.UserEmailChanged,
          metadata: '{"name":"secret@example.com","new":"other@example.com"}',
        }),
      ),
    ).toBe("Email address changed");
  });

  it("groups by local calendar days at midnight, including month and year boundaries", () => {
    const now = new Date(2027, 0, 1, 0, 5);
    const today = event({ timestamp: new Date(2027, 0, 1, 0, 1).toISOString() });
    const yesterday = event({ timestamp: new Date(2026, 11, 31, 23, 59).toISOString() });
    const earlier = event({ timestamp: new Date(2026, 11, 30, 23, 59).toISOString() });

    expect(homeActivityGroups([today, yesterday, earlier], now)).toEqual([
      { label: "Today", events: [today] },
      { label: "Yesterday", events: [yesterday] },
      { label: "Earlier", events: [earlier] },
    ]);
    expect(homeActivityGroups([today], new Date(2027, 0, 2, 0, 5))[0]!.label).toBe("Yesterday");
    expect(homeActivityGroups([], now)).toEqual([]);
  });

  it("fits grouped rows without leaving an orphan day heading", () => {
    const groups = [
      { label: "Today", events: [event()] },
      { label: "Yesterday", events: [event()] },
    ];

    expect(homeActivityDisplayRows(groups, 63)).toEqual([]);
    expect(homeActivityDisplayRows(groups, 64).map((row) => row.kind)).toEqual([
      "heading",
      "event",
    ]);
    expect(homeActivityDisplayRows(groups, 100).map((row) => row.kind)).toEqual([
      "heading",
      "event",
    ]);
    expect(homeActivityDisplayRows(groups, 128).map((row) => row.kind)).toEqual([
      "heading",
      "event",
      "heading",
      "event",
    ]);
  });

  it("formats short recent times and tolerates malformed timestamps", () => {
    const now = new Date("2026-10-08T12:00:00Z");

    expect(homeActivityTime("2026-10-08T11:59:50Z", now)).toBe("Just now");
    expect(homeActivityTime("2026-10-08T11:55:00Z", now)).toBe("5m ago");
    expect(homeActivityTime("not a date", now)).toBe("Date unavailable");
  });
});

describe("Home status and activity queries", () => {
  it("isolates owner and session and requests only the selected public status resource", async () => {
    const current = vi.spyOn(publicStatusApi, "getCurrentStatus").mockResolvedValue([]);
    const history = vi.spyOn(publicStatusApi, "getStatusHistory").mockResolvedValue([]);
    const app = createApp(defineComponent({ render: () => null }));
    app.use(createPinia());
    app.use(PiniaColada);
    const cache = app.runWithContext(() => useQueryCache());
    const options = homeSystemStatus({ userId: "alice", epoch: 1, history: true });
    const entry = cache.ensure(options);

    await cache.fetch(entry);

    expect(history).toHaveBeenCalledWith(30);
    expect(current).not.toHaveBeenCalled();
    expect(entry.state.value.data).toEqual({
      userId: "alice",
      epoch: 1,
      history: true,
      current: null,
      historyDays: [],
    });
    expect(options.key).not.toEqual(
      homeSystemStatus({ userId: "bob", epoch: 1, history: true }).key,
    );
    expect(options.key).not.toEqual(
      homeSystemStatus({ userId: "alice", epoch: 2, history: true }).key,
    );
    expect(options.key).not.toEqual(
      homeSystemStatus({ userId: "alice", epoch: 1, history: false }).key,
    );
    expect(homeSystemStatus({ userId: "", epoch: 1, history: false }).enabled).toBe(false);
    app.unmount();
  });

  it("requests a bounded newest-first activity page owned by the active account", async () => {
    const request = vi.spyOn(activityApi, "getUserActivity").mockResolvedValue(page([]));
    const app = createApp(defineComponent({ render: () => null }));
    app.use(createPinia());
    app.use(PiniaColada);
    const cache = app.runWithContext(() => useQueryCache());
    const options = homeRecentActivity({ userId: "alice", epoch: 1 });
    const entry = cache.ensure(options);

    await cache.fetch(entry);

    expect(request).toHaveBeenCalledWith({
      userId: "alice",
      page: 1,
      pageSize: 20,
      sortBy: "timestamp",
      sortDirection: SortDirection.Desc,
    });
    expect(entry.state.value.data).toEqual({ userId: "alice", epoch: 1, value: page([]) });
    expect(options.key).not.toEqual(homeRecentActivity({ userId: "bob", epoch: 1 }).key);
    expect(options.key).not.toEqual(homeRecentActivity({ userId: "alice", epoch: 2 }).key);
    expect(homeRecentActivity({ userId: "", epoch: 1 }).enabled).toBe(false);
    app.unmount();
  });
});

describe("Home status and activity component lifecycle", () => {
  it("shows unknown and retry on public status errors, and suppresses transient auth errors", async () => {
    const request = vi
      .spyOn(publicStatusApi, "getCurrentStatus")
      .mockRejectedValueOnce({ response: { status: 500 } })
      .mockResolvedValueOnce([{ serviceType: ServiceType.Api, status: "Down" }]);
    const { wrapper } = mountWidget(HomeWidgetType.SystemStatus);

    await flushPromises();

    expect(wrapper.text()).toContain("Status unknown");
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);
    expect(wrapper.text()).not.toContain("No active incidents");
    await wrapper.get("button").trigger("click");
    await flushPromises();
    expect(wrapper.text()).toContain("Service disruption");
    wrapper.unmount();

    request.mockRejectedValueOnce({ response: { status: 401 } });
    const auth = mountWidget(HomeWidgetType.SystemStatus);
    await flushPromises();
    expect(auth.wrapper.find('[aria-label="Loading widget data"]').exists()).toBe(true);
    expect(auth.wrapper.find('[role="alert"]').exists()).toBe(false);
    auth.wrapper.unmount();
  });

  it("drops previous-account activity immediately and ignores its late completion", async () => {
    let finishAlice: (value: PaginatedResponse<AuditLogResult>) => void = () => {
      throw new Error("Alice's activity request has not started");
    };
    let finishBob: (value: PaginatedResponse<AuditLogResult>) => void = () => {
      throw new Error("Bob's activity request has not started");
    };
    vi.spyOn(activityApi, "getUserActivity").mockImplementation(
      ({ userId }) =>
        new Promise((resolve) => {
          if (userId === "alice") finishAlice = resolve;
          else finishBob = resolve;
        }),
    );
    const { wrapper, store } = mountWidget(HomeWidgetType.RecentActivity);
    await flushPromises();

    store.setOwner("bob");
    await flushPromises();
    finishAlice(page([event({ metadata: '{"name":"private-alice-file.txt"}' })]));
    await flushPromises();
    expect(wrapper.text()).not.toContain("private-alice-file.txt");
    expect(wrapper.find('[aria-label="Loading widget data"]').exists()).toBe(true);

    finishBob(page([event({ userId: "bob", metadata: '{"name":"bob-file.txt"}' })]));
    await flushPromises();
    expect(wrapper.text()).toContain("bob-file.txt");
    wrapper.unmount();
  });

  it("renders named activity safely and disables its history link while editing", async () => {
    vi.spyOn(activityApi, "getUserActivity").mockResolvedValue(
      page([event({ metadata: '{"name":"<script>secret</script>"}' })]),
    );
    const { wrapper, store } = mountWidget(HomeWidgetType.RecentActivity);
    await flushPromises();

    expect(wrapper.text()).toContain("<script>secret</script>");
    expect(wrapper.find("script").exists()).toBe(false);
    store.beginEditing("desktop");
    await wrapper.vm.$nextTick();
    expect(wrapper.classes()).toContain("pointer-events-none");
    expect(wrapper.get("a").attributes("tabindex")).toBe("-1");
    wrapper.unmount();
  });

  it("shows an empty activity state rather than fabricated totals", async () => {
    vi.spyOn(activityApi, "getUserActivity").mockResolvedValue(page([]));
    const { wrapper } = mountWidget(HomeWidgetType.RecentActivity);
    await flushPromises();

    expect(wrapper.text()).toContain("No recent activity");
    expect(wrapper.find('[role="list"]').exists()).toBe(false);
    wrapper.unmount();
  });

  it("offers only the agreed status and activity styles", async () => {
    const options = createHomeWidget(HomeWidgetType.SystemStatus, "status").options;
    const status = mount(HomeSystemStatusWidgetOptions, { props: { options }, global: { stubs } });
    const activity = mount(HomeRecentActivityWidgetOptions, {
      props: { options },
      global: { stubs },
    });

    expect(status.findAll("button")).toHaveLength(3);
    expect(activity.findAll("button")).toHaveLength(2);
    await status.get('[aria-label="History strip"]').trigger("click");
    await activity.get('[aria-label="By day"]').trigger("click");
    expect(status.emitted("change")).toEqual([[{ statusStyle: HomeStatusStyle.History }]]);
    expect(activity.emitted("change")).toEqual([[{ activityStyle: HomeActivityStyle.ByDay }]]);
    status.unmount();
    activity.unmount();
  });
});
