import { afterEach, describe, expect, it, vi } from "vitest";
import { createAuthStore } from "../src/Auth/authState";
import { AuthenticationError } from "../src/services/authenticationService";
const session = () => ({ token: "opaque-test", expiresAt: Date.now() + 1000, user: { displayName: "Démo" } });
afterEach(() => vi.useRealTimers());
describe("memory-only auth session", () => {
  it("does not persist credentials or identity and clears token/session on logout", async () => {
    const local = vi.spyOn(Storage.prototype, "setItem"); const tokenSink = vi.fn();
    const store = createAuthStore({ service: { signIn: async () => session() }, tokenSink });
    await store.login({}); expect(store.getAccessToken()).toBe("opaque-test"); expect(store.getSnapshot().status).toBe("authenticated");
    expect(store.getSnapshot()).not.toHaveProperty("token"); expect(local).not.toHaveBeenCalled();
    store.logout(); expect(store.getAccessToken()).toBeNull(); expect(store.getSnapshot().user).toBeNull(); expect(tokenSink).toHaveBeenLastCalledWith(null); local.mockRestore();
  });
  it("expires at the issuer deadline and exposes unauthorized state", async () => {
    vi.useFakeTimers(); const store = createAuthStore({ service: { signIn: async () => session() }, tokenSink: vi.fn() });
    await store.login({}); await vi.advanceTimersByTimeAsync(1000);
    expect(store.getAccessToken()).toBeNull(); expect(store.getSnapshot()).toMatchObject({ status: "unauthorized", user: null });
  });
  it("prevents a late login from resurrecting a logged-out session", async () => {
    let resolve; let signal; const store = createAuthStore({ service: { signIn: (_, abortSignal) => { signal = abortSignal; return new Promise((done) => { resolve = done; }); } }, tokenSink: vi.fn() });
    const login = store.login({}); store.logout(); resolve(session()); expect(await login).toBe(false); expect(signal.aborted).toBe(true); expect(store.getSnapshot().status).toBe("anonymous");
  });
  it("invalidates on 401 while 403 leaves an authenticated session intact", async () => {
    const service = { signIn: async () => session(), authorizedRequest: vi.fn().mockRejectedValue(new AuthenticationError("forbidden", "Accès interdit")) };
    const store = createAuthStore({ service, tokenSink: vi.fn() }); await store.login({});
    await expect(store.request("/api/test")).rejects.toMatchObject({ code: "forbidden" }); expect(store.getSnapshot().status).toBe("authenticated");
    service.authorizedRequest.mockRejectedValue(new AuthenticationError("unauthorized", "Reconnectez-vous"));
    await expect(store.request("/api/test")).rejects.toMatchObject({ code: "unauthorized" }); expect(store.getSnapshot().status).toBe("unauthorized");
  });
  it("a 401 from a former request cannot invalidate a newer session", async () => {
    let reject; const store = createAuthStore({ service: { signIn: async () => session(), authorizedRequest: () => new Promise((_, fail) => { reject = fail; }) }, tokenSink: vi.fn() });
    await store.login({}); const request = store.request("/api/test"); await store.login({}); reject(new AuthenticationError("unauthorized", "Ancienne requête"));
    await expect(request).rejects.toMatchObject({ code: "unauthorized" }); expect(store.getSnapshot().status).toBe("authenticated"); store.logout();
  });
});
