import { afterEach, describe, expect, it, vi } from "vitest";
import { createDashboardService, dashboardEntities, mapDashboardActivity } from "../src/services/dashboardService";
import { createAuthenticationService } from "../src/services/authenticationService";
import { createAuthStore } from "../src/Auth/authState";
import { selectDashboard } from "../src/Dashboard/dashboardData";
const now = new Date("2026-10-17T12:00:00Z");
const event = (extra = {}) => ({ entite: 2, cleEntite: "DOS-001", keyStatut: "$CDOS", libelleStatut: "Dossier créé", dateTraceSystem: "2026-10-16T09:00:00", dateTraceUser: "2026-10-01T08:00:00", commentaire: "Événement réel du contrat", ...extra });
const response = (body, status = 200) => new Response(body === undefined ? null : JSON.stringify(body), { status });
afterEach(() => vi.useRealTimers());
describe("dashboard API contract", () => {
  it("queries the existing activity endpoint using the calendar period, entity and server limit", async () => {
    const request = vi.fn().mockResolvedValue(response([event()])); const result = await createDashboardService({ request, now: () => now }).list({ period: "7", entityType: "shipment" }); const url = new URL(request.mock.calls[0][0], "http://localhost"); expect(url.pathname).toBe("/api/v1/tracking/activite"); expect(url.searchParams.get("entites")).toBe("Dossier"); expect(url.searchParams.get("take")).toBe("500"); expect(url.searchParams.get("to")).toBe(now.toISOString()); const from = new Date(now.getFullYear(), now.getMonth(), now.getDate()); from.setDate(from.getDate() - 6); expect(url.searchParams.get("from")).toBe(from.toISOString()); expect(result.activities[0].date).toBe("2026-10-16T09:00:00.000Z"); expect(result.activities[0].businessDate).toBe("2026-10-01T08:00:00"); expect(result.asOf).toEqual(now); expect(request.mock.calls[0][1].method).toBe("GET");
  });
  it("omits the start date and entity for an unfiltered all-period request", async () => {
    const request = vi.fn().mockResolvedValue(response([])); const result = await createDashboardService({ request, now: () => now }).list({ period: "all" }); const url = new URL(request.mock.calls[0][0], "http://localhost"); expect(url.searchParams.has("from")).toBe(false); expect(url.searchParams.has("entites")).toBe(false); expect(result.activities).toEqual([]); expect(result.limited).toBe(false);
  });
  it.each(Object.entries(dashboardEntities))("supports the %s filter", async (entityType, expected) => {
    const request = vi.fn().mockResolvedValue(response([])); await createDashboardService({ request }).list({ entityType }); expect(new URL(request.mock.calls[0][0], "http://localhost").searchParams.get("entites")).toBe(expected);
  });
  it("maps numeric and named enums, raw statuses and missing labels without inventing business state", () => {
    const rows = mapDashboardActivity([event(), event({ entite: "booking", cleEntite: "BKG-001", libelleStatut: null, commentaire: "" }), event({ entite: 6, cleEntite: "COT-001", keyStatut: null, libelleStatut: null })]); expect(rows.map((row) => row.entityType)).toEqual(["shipment", "booking", "quotation"]); expect(rows[1].statusLabel).toBe("$CDOS"); expect(rows[2].statusLabel).toBe("Non renseigné"); expect(rows.every((row) => row.status === "unknown")).toBe(true); const selected = selectDashboard(rows, { period: "30", entityType: "", now }); expect(selected.active).toBeNull(); expect(selected.attention).toBeNull(); expect(selected.entities).toBe(3); expect(selected.summaries.find((row) => row.value === "shipment").unknown).toBe(1);
  });
  it("preserves distinct audit events and entity keys shared by different entity types", () => {
    const rows = mapDashboardActivity([event(), event(), event({ entite: "Booking" })]); expect(new Set(rows.map((row) => row.id)).size).toBe(3); expect(selectDashboard(rows, { period: "all", entityType: "", now }).entities).toBe(2);
  });
  it("flags the possible truncation at 500 events without inventing a total", async () => {
    const request = vi.fn().mockResolvedValue(response(Array.from({ length: 500 }, () => event()))); const result = await createDashboardService({ request }).list(); expect(result.limited).toBe(true); expect(result.limit).toBe(500); expect(result).not.toHaveProperty("totalCount");
  });
  it.each([null, {}, [null], [event({ entite: "Unknown" })], [event({ cleEntite: "" })], [event({ dateTraceSystem: "invalid" })], [event({ dateTraceSystem: "2026-02-30T09:00:00Z" })], Array.from({ length: 501 }, () => event())])("rejects invalid activity data %#", async (body) => {
    await expect(createDashboardService({ request: vi.fn().mockResolvedValue(response(body)) }).list()).rejects.toMatchObject({ code: "invalid_response" });
  });
  it("rejects an empty body or non-JSON response rather than showing zero activity", async () => {
    const request = vi.fn().mockResolvedValueOnce(response(undefined, 204)).mockResolvedValueOnce(new Response("<html>Proxy error</html>")); const service = createDashboardService({ request }); await expect(service.list()).rejects.toMatchObject({ code: "empty_response" }); await expect(service.list()).rejects.toMatchObject({ code: "invalid_response" });
  });
  it.each([[400, "validation"], [422, "validation"], [403, "forbidden"], [500, "unavailable"], [503, "unavailable"]])("maps status %s without showing technical details", async (status, code) => {
    const request = vi.fn().mockResolvedValue(response({ detail: "private SQL connection" }, status)); const service = createDashboardService({ request }); await expect(service.list()).rejects.toMatchObject({ code }); expect(request).toHaveBeenCalledTimes(1);
  });
  it("validates unsupported filters before calling the backend", async () => {
    const request = vi.fn(), service = createDashboardService({ request }); for (const options of [{ period: "999" }, { entityType: "order" }, { entityType: "tracking" }]) await expect(service.list(options)).rejects.toMatchObject({ code: "validation" }); expect(request).not.toHaveBeenCalled();
  });
  it("distinguishes network failure, timeout and caller cancellation", async () => {
    await expect(createDashboardService({ request: vi.fn().mockRejectedValue(new TypeError("network private")) }).list()).rejects.toMatchObject({ code: "network" }); vi.useFakeTimers(); const request = vi.fn((_, options) => new Promise((resolve, reject) => options.signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError"))))); const service = createDashboardService({ request, timeoutMs: 100 }); const pending = service.list(); const check = expect(pending).rejects.toMatchObject({ code: "timeout" }); await vi.advanceTimersByTimeAsync(100); await check; expect(vi.getTimerCount()).toBe(0); const controller = new AbortController(), cancelled = service.list({ signal: controller.signal }); controller.abort(); await expect(cancelled).rejects.toMatchObject({ name: "AbortError" }); expect(vi.getTimerCount()).toBe(0);
  });
  it("reuses the bearer session and invalidates it on an activity 401", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response([event()])).mockResolvedValueOnce(response(undefined, 401)); const http = createAuthenticationService({ fetcher }); const store = createAuthStore({ service: { signIn: async () => ({ token: "test-bearer", expiresAt: Date.now() + 60000, user: { displayName: "Compte Test" } }), authorizedRequest: http.authorizedRequest }, tokenSink: vi.fn() }); await store.login({});
    try { const service = createDashboardService({ request: store.request }); await service.list(); expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer test-bearer"); await expect(service.list()).rejects.toMatchObject({ code: "unauthorized" }); expect(store.getAccessToken()).toBeNull(); } finally { store.logout(); }
  });
});
