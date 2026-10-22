import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
import { AuthenticationError } from "../src/services/authenticationService";
vi.mock("../src/components/Layout", () => ({ default: () => <p>Dashboard protégé</p> }));
vi.mock("../src/Chat/ChatPage", () => ({ default: () => <p>Chat protégé</p> }));
let store;
beforeEach(() => { vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); });
afterEach(() => { store?.logout(); window.history.replaceState(null, "", "/"); vi.unstubAllGlobals(); });
const session = { token: "demo", expiresAt: Date.now() + 60000, user: { displayName: "Compte de test" } };
const submit = () => { fireEvent.change(screen.getByLabelText("Identifiant"), { target: { value: "demo" } }); fireEvent.change(screen.getByLabelText("Mot de passe"), { target: { value: "fictif" } }); fireEvent.click(screen.getByRole("button", { name: "Se connecter" })); };
describe("protected application routes", () => {
  it.each(["#main-content", "#chat", "#agent", "#knowledge-base", "#mcp-tools"])("redirects anonymous visitors from %s before mounting protected pages", async (hash) => {
    window.history.replaceState(null, "", hash); store = createAuthStore({ tokenSink: vi.fn() }); render(<App authStore={store} />);
    expect(screen.getByRole("heading", { name: "Connexion" })).toBeTruthy(); expect(window.location.hash).toBe("#login"); expect(screen.queryByRole("navigation", { name: "Navigation principale" })).toBeNull();
  });
  it("shows loading, clears the password and returns to the requested route after login; logout protects it again", async () => {
    let resolve; const signIn = vi.fn(() => new Promise((done) => { resolve = done; }));
    store = createAuthStore({ service: { signIn }, tokenSink: vi.fn() }); window.history.replaceState(null, "", "#chat"); render(<App authStore={store} />); submit();
    expect(screen.getByRole("status", { name: "Connexion en cours…" })).toBeTruthy(); expect(screen.getByLabelText("Mot de passe").value).toBe(""); expect(screen.queryByText("Chat protégé")).toBeNull();
    await act(async () => resolve(session)); await waitFor(() => expect(screen.getByText("Chat protégé")).toBeTruthy()); expect(window.location.hash).toBe("#chat");
    fireEvent.click(screen.getByRole("button", { name: "Se déconnecter" })); expect(screen.getByRole("heading", { name: "Connexion" })).toBeTruthy(); expect(screen.queryByText("Chat protégé")).toBeNull(); expect(store.getAccessToken()).toBeNull();
  });
  it("shows unauthorized errors without exposing the server or mounting private pages", async () => {
    store = createAuthStore({ service: { signIn: async () => { throw new AuthenticationError("unauthorized", "Identifiant ou mot de passe incorrect."); } }, tokenSink: vi.fn() });
    window.history.replaceState(null, "", "#login"); render(<App authStore={store} />); submit();
    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("Accès non autorisé")); expect(screen.queryByText("Dashboard protégé")).toBeNull(); expect(screen.getByRole("button", { name: "Se connecter" }).disabled).toBe(true);
  });
  it("redirects an invalid destination to the dashboard after authentication", async () => {
    store = createAuthStore({ service: { signIn: async () => session }, tokenSink: vi.fn() }); window.history.replaceState(null, "", "#https://foreign.invalid"); render(<App authStore={store} />); submit();
    await waitFor(() => expect(screen.getByRole("heading", { name: "Dashboard", exact: true })).toBeTruthy()); expect(window.location.hash).toBe("#main-content");
  });
});

