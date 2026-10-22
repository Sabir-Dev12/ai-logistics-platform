import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createAuthenticationService } from "../src/services/authenticationService";
import { createAuthStore } from "../src/Auth/authState";
import AuthProvider from "../src/Auth/AuthProvider";
import LoginPage from "../src/Auth/LoginPage";
const credentials = { identifiant: " demo ", motDePasse: " fictif " };
const token = { accessToken: "opaque-test", tokenType: "Bearer", expiresAt: "2099-01-01T00:00:00Z", societeKey: "S01" };
const response = (body, status = 200) => new Response(body === undefined ? null : JSON.stringify(body), { status });
let store;
afterEach(() => { store?.logout(); vi.useRealTimers(); });
describe("authentication API errors", () => {
  it("validates empty input before making HTTP calls", async () => {
    const fetcher = vi.fn(); await expect(createAuthenticationService({ fetcher }).signIn({ identifiant: " ", motDePasse: "" })).rejects.toMatchObject({ code: "validation", fieldErrors: { identifiant: "L’identifiant est obligatoire.", motDePasse: "Le mot de passe est obligatoire." } }); expect(fetcher).not.toHaveBeenCalled();
  });
  it.each([400, 422, 429, 500, 503])("maps HTTP %s without leaking server diagnostics or retrying automatically", async (status) => {
    const fetcher = vi.fn().mockResolvedValue(response({ detail: "private database detail", errors: { Identifiant: ["private"] } }, status)); const service = createAuthenticationService({ fetcher }); await expect(service.signIn(credentials)).rejects.toMatchObject({ code: status === 400 || status === 422 ? "validation" : status === 429 ? "rate_limit" : "unavailable" }); expect(fetcher).toHaveBeenCalledTimes(1);
  });
  it.each([undefined, "", null, [], {}])("rejects empty or malformed session %j", async (body) => {
    const fetcher = vi.fn().mockResolvedValue(response(body)); await expect(createAuthenticationService({ fetcher }).signIn(credentials)).rejects.toMatchObject({ code: body === undefined ? "empty_response" : "invalid_response", retryable: true }); expect(fetcher).toHaveBeenCalledTimes(1);
  });
  it("rejects a non-JSON response and an empty identity without publishing a session", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(new Response("<html>Proxy</html>")); await expect(createAuthenticationService({ fetcher }).signIn(credentials)).rejects.toMatchObject({ code: "invalid_response" }); fetcher.mockResolvedValueOnce(response(token)).mockResolvedValueOnce(new Response(null, { status: 204 })); store = createAuthStore({ service: createAuthenticationService({ fetcher }), tokenSink: vi.fn() }); expect(await store.login(credentials)).toBe(false); expect(store.getAccessToken()).toBeNull(); expect(store.getSnapshot().retryable).toBe(true);
  });
  it("rejects blank or inconsistent identity companies", async () => {
    for (const societeKey of ["", " ", "S99"]) { const fetcher = vi.fn().mockResolvedValueOnce(response(token)).mockResolvedValueOnce(response({ societeKey })); await expect(createAuthenticationService({ fetcher }).signIn(credentials)).rejects.toMatchObject({ code: "invalid_response" }); }
  });
  it("supports configured company context without exposing it as a UI input", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response(token)).mockResolvedValueOnce(response({ societeKey: "S01" })); await createAuthenticationService({ fetcher, companyKey: " S01 " }).signIn(credentials); expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({ identifiant: "demo", motDePasse: " fictif ", societeKey: "S01" });
  });
  it("distinguishes a network failure from a server error", async () => {
    const fetcher = vi.fn().mockRejectedValue(new TypeError("fetch failed")); await expect(createAuthenticationService({ fetcher }).signIn(credentials)).rejects.toMatchObject({ code: "network", retryable: true });
  });
  it("times out hanging requests and clears pending timers on success", async () => {
    vi.useFakeTimers(); const fetcher = vi.fn((_, options) => new Promise((resolve, reject) => { options.signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError"))); })); const pending = createAuthenticationService({ fetcher, timeoutMs: 100 }).signIn(credentials); const assertion = expect(pending).rejects.toMatchObject({ code: "timeout", retryable: true }); await vi.advanceTimersByTimeAsync(100); await assertion; expect(vi.getTimerCount()).toBe(0);
  });
  it("propagates user cancellation without turning it into a network error", async () => {
    const controller = new AbortController(); const fetcher = vi.fn((_, options) => new Promise((resolve, reject) => options.signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError"))))); const pending = createAuthenticationService({ fetcher }).signIn(credentials, controller.signal); controller.abort(); await expect(pending).rejects.toMatchObject({ name: "AbortError" });
  });
  it.each([[400, "Les informations de connexion sont invalides."], [401, "Identifiant ou mot de passe incorrect."], [403, "Vous n’avez pas accès"], [503, "indisponible"], [204, "réponse vide"]])("renders the API %s error with the existing UI", async (status, message) => {
    store = createAuthStore({ service: createAuthenticationService({ fetcher: vi.fn().mockResolvedValue(response(undefined, status)) }), tokenSink: vi.fn() }); render(<AuthProvider store={store}><LoginPage onAuthenticated={vi.fn()} /></AuthProvider>); fireEvent.change(screen.getByLabelText("Identifiant"), { target: { value: "demo" } }); fireEvent.change(screen.getByLabelText("Mot de passe"), { target: { value: "fictif" } }); fireEvent.click(screen.getByRole("button", { name: "Se connecter" })); await waitFor(() => expect(screen.getByRole("alert").textContent).toContain(message)); expect(screen.getByLabelText("Mot de passe").value).toBe(""); expect(store.getAccessToken()).toBeNull();
  });
  it("retries manually after network failure and publishes success only after identity lookup", async () => {
    const fetcher = vi.fn().mockRejectedValueOnce(new TypeError("Network")); const onAuthenticated = vi.fn(); store = createAuthStore({ service: createAuthenticationService({ fetcher }), tokenSink: vi.fn() }); render(<AuthProvider store={store}><LoginPage onAuthenticated={onAuthenticated} /></AuthProvider>); fireEvent.change(screen.getByLabelText("Identifiant"), { target: { value: "demo" } }); fireEvent.change(screen.getByLabelText("Mot de passe"), { target: { value: "fictif" } }); fireEvent.click(screen.getByRole("button", { name: "Se connecter" })); await screen.findByText(/Ressaisissez votre mot de passe/); expect(fetcher).toHaveBeenCalledTimes(1); fetcher.mockResolvedValueOnce(response(token)).mockResolvedValueOnce(response({ societeKey: "S01", userDisplayName: "Compte Test" })); fireEvent.change(screen.getByLabelText("Mot de passe"), { target: { value: "fictif" } }); fireEvent.click(screen.getByRole("button", { name: "Se connecter" })); await waitFor(() => expect(onAuthenticated).toHaveBeenCalled()); expect(store.getSnapshot()).toMatchObject({ status: "authenticated", retryable: false, error: null }); expect(fetcher).toHaveBeenCalledTimes(3);
  });
  it("shows empty-form guidance using the existing field error styles", async () => {
    store = createAuthStore({ tokenSink: vi.fn() }); render(<AuthProvider store={store}><LoginPage onAuthenticated={vi.fn()} /></AuthProvider>); await act(async () => { await store.login({}); }); expect(screen.getByLabelText("Identifiant").getAttribute("aria-invalid")).toBe("true"); expect(screen.getByText("Le mot de passe est obligatoire.")).toBeTruthy();
  });
});
