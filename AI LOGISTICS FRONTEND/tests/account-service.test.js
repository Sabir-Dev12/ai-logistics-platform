import { afterEach, describe, expect, it, vi } from "vitest";
import { createAccountService } from "../src/services/accountService";
import { createAuthenticationService } from "../src/services/authenticationService";
import { createAuthStore } from "../src/Auth/authState";
const user = { societeKey: "S01", userKey: "P001", userDisplayName: "Compte Test" };
const company = { keySoc: "S01", intituleSoc: "Logistics Test" };
const password = { oldPassword: " AncienTest123! ", newPassword: " NouveauTest123! " };
const response = (body, status = 200) => new Response(body === undefined ? null : JSON.stringify(body), { status });
afterEach(() => vi.useRealTimers());
describe("account API contract", () => {
  it("loads identity and company with no technical company reference in the view model", async () => {
    const request = vi.fn().mockResolvedValueOnce(response(user)).mockResolvedValueOnce(response(company)); const service = createAccountService({ request, logout: vi.fn() }); expect(await service.getProfile()).toEqual({ displayName: "Compte Test", identifier: "P001", companyName: "Logistics Test", companyWarning: null }); expect(request.mock.calls.map(([path]) => path)).toEqual(["/api/v1/auth/moi", "/api/v1/reference/societe"]);
  });
  it("handles empty identity without querying company, rejects malformed identity and JSON", async () => {
    const request = vi.fn().mockResolvedValueOnce(response(undefined, 204)); const service = createAccountService({ request, logout: vi.fn() }); expect(await service.getProfile()).toBeNull(); expect(request).toHaveBeenCalledTimes(1); request.mockResolvedValueOnce(response({})); await expect(service.getProfile()).rejects.toMatchObject({ code: "invalid_response" }); request.mockResolvedValueOnce(new Response("<html>Proxy</html>")); await expect(service.getProfile()).rejects.toMatchObject({ code: "invalid_response" });
  });
  it.each([204, 403, 404, 503])("keeps valid identity when company returns %s", async (status) => {
    const request = vi.fn().mockResolvedValueOnce(response(user)).mockResolvedValueOnce(response(undefined, status)); const profile = await createAccountService({ request, logout: vi.fn() }).getProfile(); expect(profile.identifier).toBe("P001"); expect(profile.companyName).toBe("Non disponible"); expect(profile.companyWarning).toBeTruthy();
  });
  it("never shows another company's name", async () => {
    const request = vi.fn().mockResolvedValueOnce(response(user)).mockResolvedValueOnce(response({ keySoc: "S99", intituleSoc: "Other tenant" })); const result = await createAccountService({ request, logout: vi.fn() }).getProfile(); expect(result.companyName).toBe("Non disponible");
  });
  it("maps the password payload exactly and accepts 204 without attempting to parse a body", async () => {
    const request = vi.fn().mockResolvedValue(response(undefined, 204)); await createAccountService({ request, logout: vi.fn() }).changePassword(password); expect(request.mock.calls[0][0]).toBe("/api/v1/auth/mot-de-passe"); expect(JSON.parse(request.mock.calls[0][1].body)).toEqual({ ancienMotDePasse: password.oldPassword, nouveauMotDePasse: password.newPassword }); expect(request.mock.calls[0][1].method).toBe("POST"); expect(request).toHaveBeenCalledTimes(1);
  });
  it.each([[400, "validation"], [403, "forbidden"], [404, "not_found"], [422, "validation"], [429, "rate_limit"], [503, "unavailable"], [200, "invalid_response"]])("maps password status %s without leaking details or retrying the mutation", async (status, code) => {
    const request = vi.fn().mockResolvedValue(response({ detail: "private diagnostic" }, status)); await expect(createAccountService({ request, logout: vi.fn() }).changePassword(password)).rejects.toMatchObject({ code }); expect(request).toHaveBeenCalledTimes(1);
  });
  it("validates invalid password fields before any HTTP request", async () => {
    const request = vi.fn(); const service = createAccountService({ request, logout: vi.fn() }); for (const value of [{ oldPassword: "", newPassword: "Valide123!" }, { oldPassword: "old", newPassword: "short" }, { oldPassword: "old", newPassword: " ".repeat(8) }, { oldPassword: "old", newPassword: "a".repeat(201) }]) await expect(service.changePassword(value)).rejects.toMatchObject({ code: "validation" }); expect(request).not.toHaveBeenCalled();
  });
  it("keeps a valid session after a wrong current password and invalidates a genuinely expired one", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response(undefined, 401)).mockResolvedValueOnce(response(user));
    const http = createAuthenticationService({ fetcher }); const store = createAuthStore({ service: { signIn: async () => ({ token: "unit-test", expiresAt: Date.now() + 60000, user: { displayName: "Compte Test" } }), authorizedRequest: http.authorizedRequest }, tokenSink: vi.fn() }); await store.login({});
    try {
      const service = createAccountService({ request: store.request, logout: store.logout }); await expect(service.changePassword(password)).rejects.toMatchObject({ code: "old_password_invalid" }); expect(store.getSnapshot().status).toBe("authenticated"); expect(fetcher.mock.calls[0][1]).not.toHaveProperty("acceptPasswordRejection");
      fetcher.mockResolvedValueOnce(response(undefined, 401)).mockResolvedValueOnce(response(undefined, 401)); await expect(service.changePassword(password)).rejects.toMatchObject({ code: "unauthorized" }); expect(store.getSnapshot().status).toBe("unauthorized"); expect(store.getAccessToken()).toBeNull();
    } finally { store.logout(); }
  });
  it("does not suppress a 401 from any other endpoint or method", async () => {
    const http = createAuthenticationService({ fetcher: vi.fn().mockResolvedValue(response(undefined, 401)) }); for (const [url, method] of [["/api/v1/auth/moi", "GET"], ["/api/v1/auth/mot-de-passe", "GET"], ["/api/v1/clients", "POST"]]) await expect(http.authorizedRequest(url, "unit-test", { method, acceptPasswordRejection: true })).rejects.toMatchObject({ code: "unauthorized" });
  });
  it("times out without automatically replaying the password request", async () => {
    vi.useFakeTimers(); const request = vi.fn((_, options) => new Promise((resolve, reject) => options.signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError"))))); const pending = createAccountService({ request, logout: vi.fn(), timeoutMs: 100 }).changePassword(password); const assertion = expect(pending).rejects.toMatchObject({ code: "timeout" }); await vi.advanceTimersByTimeAsync(100); await assertion; expect(request).toHaveBeenCalledTimes(1); expect(vi.getTimerCount()).toBe(0);
  });
  it("handles network failure, propagates cancellation and logs out locally without an invented endpoint", async () => {
    const request = vi.fn().mockRejectedValue(new TypeError("private network error")), logout = vi.fn(); const service = createAccountService({ request, logout }); await expect(service.getProfile()).rejects.toMatchObject({ code: "network" }); const controller = new AbortController(); controller.abort(); await expect(service.getProfile({ signal: controller.signal })).rejects.toMatchObject({ name: "AbortError" }); request.mockClear(); await service.logout(); expect(logout).toHaveBeenCalledTimes(1); expect(request).not.toHaveBeenCalled();
  });
});
