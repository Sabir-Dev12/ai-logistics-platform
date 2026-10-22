// @vitest-environment node
import { describe, expect, it } from "vitest";
import process from "node:process";
import { createAuthenticationService } from "../src/services/authenticationService";
const base = process.env.AUTH_SIMULATION_URL;
const service = (timeoutMs = 2000) => createAuthenticationService({ fetcher: (path, options) => fetch(base + path, options), timeoutMs });
describe.skipIf(!base)("authentication through real HTTP against the Postman simulation", () => {
  it("exchanges credentials and loads identity through two real HTTP requests", async () => { const result = await service().signIn({ identifiant: "success", motDePasse: "fictif" }); expect(result.user.displayName).toBe("Compte de simulation"); expect(result.token).toBe("simulation-only-success"); });
  it.each([["validation", "validation"], ["server", "unavailable"], ["forbidden", "forbidden"], ["empty", "empty_response"], ["malformed", "invalid_response"], ["null", "invalid_response"], ["identity-empty", "empty_response"], ["identity-unauthorized", "unauthorized"]])("handles %s over HTTP", async (identifiant, code) => { await expect(service().signIn({ identifiant, motDePasse: "fictif" })).rejects.toMatchObject({ code }); });
  it("rejects invalid credentials", async () => { await expect(service().signIn({ identifiant: "success", motDePasse: "incorrect" })).rejects.toMatchObject({ code: "unauthorized" }); });
  it("limits waiting on a slow HTTP response", async () => { await expect(service(50).signIn({ identifiant: "slow", motDePasse: "fictif" })).rejects.toMatchObject({ code: "timeout" }); });
  it("reports a connection refusal as a network failure", async () => { const offline = createAuthenticationService({ fetcher: (path, options) => fetch("http://127.0.0.1:4919" + path, options), timeoutMs: 1000 }); await expect(offline.signIn({ identifiant: "success", motDePasse: "fictif" })).rejects.toMatchObject({ code: "network" }); });
});

