import { describe, expect, it, vi } from "vitest";
import { createAuthenticationService } from "../src/services/authenticationService";
const credentials = { identifiant: " demo ", motDePasse: "fictif", societeKey: " S01 " };
const token = { accessToken: "opaque-test-token", tokenType: "Bearer", expiresAt: "2099-01-01T00:00:00" };
const response = (status, body) => new Response(body === undefined ? null : JSON.stringify(body), { status });

describe("authentication HTTP contract", () => {
  it("exchanges credentials then verifies identity, preserves the password and reads unspecified UTC expiry", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response(200, token)).mockResolvedValueOnce(response(200, { societeKey: "S01", userDisplayName: "Démo", userKey: "secret-unused" }));
    const result = await createAuthenticationService({ fetcher }).signIn(credentials);
    expect(fetcher.mock.calls[0][0]).toBe("/api/v1/auth/token");
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({ identifiant: "demo", motDePasse: "fictif", societeKey: "S01" });
    expect(fetcher.mock.calls[1][0]).toBe("/api/v1/auth/moi");
    expect(fetcher.mock.calls[1][1].headers.Authorization).toBe("Bearer opaque-test-token");
    expect(result.user).toEqual({ displayName: "Démo", societeKey: "S01" });
    expect(result.expiresAt).toBe(Date.parse("2099-01-01T00:00:00Z"));
  });
  it.each([[401, "unauthorized"], [403, "forbidden"], [503, "unavailable"]])("handles %s with a safe error", async (status, code) => {
    const fetcher = vi.fn().mockResolvedValue(response(status, { detail: "private server details" }));
    const service = createAuthenticationService({ fetcher });
    await expect(service.signIn(credentials)).rejects.toMatchObject({ code });
    try { await service.signIn(credentials); } catch (error) { expect(error.message).not.toContain("private"); }
  });
  it("rejects malformed/expired tokens before identity lookup and rejects an unauthorized identity", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response(200, { ...token, expiresAt: "2020-01-01" }));
    const service = createAuthenticationService({ fetcher });
    await expect(service.signIn(credentials)).rejects.toMatchObject({ code: "invalid_response" }); expect(fetcher).toHaveBeenCalledTimes(1);
    fetcher.mockResolvedValueOnce(response(200, token)).mockResolvedValueOnce(response(401));
    await expect(service.signIn(credentials)).rejects.toMatchObject({ code: "unauthorized" });
  });
  it("does not send a token to a foreign origin or follow redirects", async () => {
    const fetcher = vi.fn().mockResolvedValue(response(200, {})); const service = createAuthenticationService({ fetcher });
    await expect(service.authorizedRequest("https://foreign.invalid/api/data", "test")).rejects.toMatchObject({ code: "invalid_target" });
    expect(fetcher).not.toHaveBeenCalled();
    await service.authorizedRequest("/api/v1/auth/moi", "test");
    expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer test");
    expect(fetcher.mock.calls[0][1]).toMatchObject({ cache: "no-store", credentials: "omit", redirect: "error" });
  });
});

