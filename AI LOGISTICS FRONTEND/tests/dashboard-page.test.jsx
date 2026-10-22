import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import DashboardPage from "../src/Dashboard/DashboardPage";
import { createMockActivities, selectDashboard } from "../src/Dashboard/dashboardData";
import { DashboardError } from "../src/services/dashboardService";

vi.mock("../src/components/Layout", () => ({ default: ({ data }) => <output aria-label="Graphiques">{JSON.stringify(data)}</output> }));
const now = new Date("2026-10-15T12:00:00Z");
const event = (id, entityId, status, date, entityType = "shipment") => ({ id, entityId, status, date, entityType, description: `Activité ${id}` });
const fixture = [event("new", "EXP-001", "active", "2026-10-15T09:00:00Z"),
  event("old", "EXP-001", "pending", "2026-10-14T09:00:00Z"),
  event("booking", "BKG-001", "complete", "2026-10-01T09:00:00Z", "booking")];

describe("dashboard business summaries", () => {
  it("counts activity events separately from unique entities and uses the latest status", () => {
    const result = selectDashboard(fixture, { period: "30", entityType: "", now });
    expect(result.activities).toHaveLength(3); expect(result.entities).toBe(2); expect(result.active).toBe(1);
    const shipment = result.summaries.find((item) => item.value === "shipment");
    expect(shipment.total).toBe(1); expect(shipment.pending).toBe(0); expect(shipment.active).toBe(1);
    expect(result.chartData.reduce((sum, day) => sum + day.uv + day.pv + day.amt, 0)).toBe(3);
  });
  it("combines entity and calendar-period filters, includes the boundary day, excludes future events", () => {
    const today = new Date(2026, 9, 15, 12), boundary = new Date(2026, 9, 9), before = new Date(2026, 9, 8, 23, 59);
    const rows = [event("boundary", "A", "active", boundary.toISOString()), event("before", "B", "active", before.toISOString()),
      event("future", "C", "active", new Date(2026, 9, 16).toISOString()), event("other", "D", "complete", boundary.toISOString(), "booking")];
    expect(selectDashboard(rows, { period: "7", entityType: "shipment", now: today }).activities.map((row) => row.id)).toEqual(["boundary"]);
    expect(selectDashboard(rows, { period: "all", entityType: "shipment", now: today }).activities).toHaveLength(2);
  });
  it("keeps charts chronological and does not merge the same date across years", () => {
    const rows = [event("new", "A", "active", "2026-10-15T09:00:00Z"), event("old", "B", "active", "2025-10-15T09:00:00Z")];
    const result = selectDashboard(rows, { period: "all", entityType: "", now });
    expect(result.activities[0].id).toBe("new"); expect(result.chartData).toHaveLength(2);
    expect(result.chartData[0].name).not.toBe(result.chartData[1].name);
  });
});

describe("dashboard page", () => {
  it("shows raw backend statuses, unavailable classifications and the feed limit", async () => {
    const rows = [{ id: "real-1", entityType: "shipment", entityId: "DOS-001", status: "unknown", statusLabel: "Dossier créé", description: "Création auditée", date: new Date().toISOString() }];
    render(<DashboardPage repository={{ list: async () => ({ activities: rows, asOf: new Date(), realStatuses: true, limit: 500, limited: true }) }} />);
    const activity = await screen.findByRole("table", { name: "Activités de la période sélectionnée" }); expect(within(activity).getByText("Dossier créé")).toBeTruthy(); expect(screen.getByText(/La limite est atteinte/)).toBeTruthy(); expect(screen.getByRole("heading", { name: "En cours", exact: true }).parentElement.textContent).toContain("—"); expect(screen.getByRole("heading", { name: "À vérifier", exact: true }).parentElement.textContent).toContain("—");
  });
  it("refetches with new filters, aborts the former request and ignores its late response", async () => {
    let firstSignal, resolveFirst; const rows = createMockActivities().filter((row) => row.entityType === "booking");
    const repository = { list: vi.fn().mockImplementationOnce(({ signal }) => { firstSignal = signal; return new Promise((done) => { resolveFirst = done; }); }).mockResolvedValueOnce(rows) };
    render(<DashboardPage repository={repository} />); await waitFor(() => expect(repository.list).toHaveBeenCalledOnce()); fireEvent.change(screen.getByLabelText("Type d’entité"), { target: { value: "booking" } }); const table = await screen.findByRole("table", { name: "Activités de la période sélectionnée" }); expect(firstSignal.aborted).toBe(true); expect(repository.list.mock.calls[1][0]).toMatchObject({ period: "30", entityType: "booking" }); await act(async () => resolveFirst([{ ...rows[0], description: "Obsolete response" }])); expect(screen.queryByText("Obsolete response")).toBeNull(); expect(within(table).getAllByText("Réservation").length).toBeGreaterThan(0);
  });
  it.each([["validation", "Filtres invalides."], ["network", "Réseau indisponible."], ["unavailable", "Service indisponible."]])("renders the API %s failure and retries without stale metrics", async (code, message) => {
    const repository = { list: vi.fn().mockRejectedValueOnce(new DashboardError(code, message)).mockResolvedValueOnce([]) };
    render(<DashboardPage repository={repository} />); await screen.findByText(message); expect(screen.queryByLabelText("Synthèses de l’activité filtrée")).toBeNull(); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await screen.findByText("Aucune activité"); expect(repository.list).toHaveBeenCalledTimes(2);
  });
  it("shows loading without stale metrics then renders mock activity, statuses and financial availability", async () => {
    let resolve;
    const repository = { list: vi.fn(() => new Promise((done) => { resolve = done; })) };
    render(<DashboardPage repository={repository} />);
    expect(screen.getByRole("status", { name: "Chargement du dashboard…" })).toBeTruthy();
    expect(screen.queryByRole("table")).toBeNull();
    await act(async () => resolve(createMockActivities()));
    expect(screen.getByRole("table", { name: "Synthèse des entités concernées" })).toBeTruthy();
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
    expect(screen.getByText("Indicateurs financiers indisponibles")).toBeTruthy();
    expect(screen.queryByText(/€|MAD/)).toBeNull();
  });
  it("filters activity and charts together and resets pagination when a filter changes", async () => {
    render(<DashboardPage repository={{ list: async () => createMockActivities() }} />);
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
    fireEvent.click(screen.getByRole("button", { name: "Suivant" }));
    expect(screen.getByRole("button", { name: "2" }).getAttribute("aria-current")).toBe("page");
    fireEvent.change(screen.getByLabelText("Type d’entité"), { target: { value: "shipment" } });
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
    expect(screen.getByRole("button", { name: "1" }).getAttribute("aria-current")).toBe("page");
    const table = screen.getByRole("table", { name: "Activités de la période sélectionnée" });
    expect(within(table).queryByText("Réservation")).toBeNull();
    expect(within(table).getAllByText("Expédition").length).toBeGreaterThan(0);
    const charts = JSON.parse(screen.getByLabelText("Graphiques").textContent);
    expect(charts.every((day) => day.pv === 0 && day.amt === 0)).toBe(true);
    fireEvent.change(screen.getByLabelText("Période"), { target: { value: "7" } });
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
    expect(within(screen.getByRole("table", { name: "Activités de la période sélectionnée" })).getAllByRole("row")).toHaveLength(3);
  });
  it("shows an empty state for no activities rather than synthetic zero metrics", async () => {
    render(<DashboardPage repository={{ list: async () => [] }} />);
    await screen.findByText("Aucune activité");
    expect(screen.queryByRole("table")).toBeNull(); expect(screen.queryByLabelText("Graphiques")).toBeNull();
    expect(screen.queryByLabelText("Synthèses de l’activité filtrée")).toBeNull();
  });
  it("allows resetting filters after a query has no results", async () => {
    const rows = createMockActivities().filter((row) => row.entityType === "shipment");
    render(<DashboardPage repository={{ list: async () => rows }} />);
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
    fireEvent.change(screen.getByLabelText("Type d’entité"), { target: { value: "booking" } });
    await screen.findByText("Aucun résultat");
    fireEvent.click(within(screen.getByText("Aucun résultat").parentElement).getByRole("button", { name: "Réinitialiser les filtres" }));
    expect(screen.getByLabelText("Type d’entité").value).toBe("");
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
  });
  it("hides provider details, offers retry, and clears stale content while refreshing", async () => {
    const repository = { list: vi.fn().mockRejectedValueOnce(new Error("password=secret"))
      .mockResolvedValueOnce(createMockActivities()).mockImplementationOnce(() => new Promise(() => {})) };
    render(<DashboardPage repository={repository} />);
    await screen.findByRole("alert");
    expect(screen.queryByText(/password=secret/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Réessayer" }));
    await screen.findByRole("table", { name: "Activités de la période sélectionnée" });
    fireEvent.click(screen.getByRole("button", { name: "Actualiser" }));
    expect(screen.getByRole("status", { name: "Chargement du dashboard…" })).toBeTruthy();
    expect(screen.queryByRole("table")).toBeNull();
  });
  it("aborts loading when unmounted and ignores a late response", async () => {
    let signal, resolve;
    const repository = { list: vi.fn((options) => { signal = options.signal; return new Promise((done) => { resolve = done; }); }) };
    const view = render(<DashboardPage repository={repository} />);
    await waitFor(() => expect(repository.list).toHaveBeenCalledOnce());
    view.unmount(); expect(signal.aborted).toBe(true);
    await act(async () => resolve(createMockActivities()));
  });
});

