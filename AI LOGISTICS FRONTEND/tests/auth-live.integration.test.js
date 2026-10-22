// @vitest-environment node
import process from "node:process";
import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";
import { createAuthenticationService } from "../src/services/authenticationService";
const base = process.env.AUTH_REAL_BACKEND_URL;
const configPath = process.env.AUTH_REAL_CONFIG_PATH;
describe.skipIf(!base || !configPath)("authentication against the local backend", () => {
  it("authenticates the configured development account and verifies its identity without logging credentials", async () => {
    const config = JSON.parse(await readFile(configPath, "utf8")); const account = config.Authentication.ServiceAccount;
    const service = createAuthenticationService({ fetcher: (path, options) => fetch(base + path, options), companyKey: account.SocieteKey });
    const session = await service.signIn({ identifiant: account.Identifiant, motDePasse: account.MotDePasse });
    expect(Boolean(session.token)).toBe(true); expect(session.expiresAt > Date.now()).toBe(true); expect(session.user.societeKey === account.SocieteKey).toBe(true);
    await expect(service.signIn({ identifiant: account.Identifiant, motDePasse: "intentionally-invalid-local-test-password" })).rejects.toMatchObject({ code: "unauthorized" });
  }, 25000);
});
