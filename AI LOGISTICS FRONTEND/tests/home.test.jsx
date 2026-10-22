import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import HomePage from "../src/Home/HomePage";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
import { createMockActivities } from "../src/Dashboard/dashboardData";
const preferences = vi.hoisted(() => ({ reduced: false }));
vi.mock("motion/react", async (original) => ({ ...await original(), useReducedMotion: () => preferences.reduced }));
vi.mock("../src/Home/BackgroundVideo", () => ({ default: (props) => <div data-testid="hero-media" data-playing={String(props.playing)} /> }));
vi.mock("../src/components/Layout", () => ({ default: () => <div>Graphiques du dashboard</div> }));
const navigation = [{ href: "#main-content", label: "Dashboard" }, { href: "#clients", label: "Clients" }, { href: "#bookings", label: "Bookings / Réservations" }, { href: "#chat", label: "AI Chatbot" }, { href: "#knowledge-base", label: "Knowledge Base" }, { href: "#mcp-tools", label: "MCP Tools" }, { href: "#agent", label: "AI Agent" }, { href: "#account", label: "Mon compte" }];
const repository = { list: async () => createMockActivities() };
const home = (props = {}) => <HomePage navigation={navigation} onLogout={vi.fn()} repository={repository} {...props} />;
beforeEach(() => {
  preferences.reduced = false; window.history.replaceState(null, "", "/#home");
  vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn(), addListener: vi.fn(), removeListener: vi.fn() })));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });
describe("dashboard home", () => {
  it("replaces the marketing content with the existing dashboard and statistics", async () => {
    render(home());
    await screen.findByLabelText("Synthèses de l’activité filtrée");
    expect(screen.getByRole("heading", { name: "Dashboard", level: 1 })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Entités concernées" })).toBeTruthy();
    expect(screen.getByLabelText("Période")).toBeTruthy();
    expect(screen.getByText("Graphiques du dashboard")).toBeTruthy();
    expect(screen.queryByText("Build Faster")).toBeNull();
    expect(screen.queryByText("Start Building Free")).toBeNull();
    expect(screen.queryByText("See Examples")).toBeNull();
  });
  it("links every application page instead of marketing options", async () => {
    render(home());
    const nav = screen.getByRole("navigation", { name: "Pages de l’application" });
    expect(within(nav).getAllByRole("link")).toHaveLength(8);
    for (const item of navigation) expect(within(nav).getByRole("link", { name: item.label, exact: true }).getAttribute("href")).toBe(item.href === "#main-content" ? "#home" : item.href);
    expect(screen.queryByText("Pricing")).toBeNull(); expect(screen.queryByText("Book A Demo")).toBeNull(); expect(screen.queryByText("Get Started")).toBeNull();
    await screen.findByLabelText("Synthèses de l’activité filtrée");
  });
  it("opens all pages on mobile and closes navigation after selection", async () => {
    render(home()); fireEvent.click(screen.getByRole("button", { name: "Ouvrir les pages" }));
    const modal = screen.getByRole("dialog", { name: "Pages de l’application" });
    expect(within(modal).getAllByRole("link")).toHaveLength(8);
    fireEvent.click(within(modal).getByRole("link", { name: "Knowledge Base", exact: true }));
    expect(screen.queryByRole("dialog")).toBeNull();
    await screen.findByLabelText("Synthèses de l’activité filtrée");
  });
  it("uses the existing logout callback", async () => {
    const logout = vi.fn(); render(home({ onLogout: logout, displayName: "Compte de test" }));
    fireEvent.click(screen.getByRole("button", { name: "Se déconnecter" })); expect(logout).toHaveBeenCalledOnce();
    await screen.findByLabelText("Synthèses de l’activité filtrée");
  });
  it("pauses the background and respects reduced motion", async () => {
    const view = render(home());
    fireEvent.click(screen.getByRole("button", { name: "Pause background video" })); expect(screen.getByTestId("hero-media").dataset.playing).toBe("false");
    fireEvent.click(screen.getByRole("button", { name: "Play background video" })); expect(screen.getByTestId("hero-media").dataset.playing).toBe("true");
    await screen.findByLabelText("Synthèses de l’activité filtrée");
    view.unmount(); preferences.reduced = true; render(home());
    expect(screen.getByTestId("hero-media").dataset.playing).toBe("false"); expect(screen.queryByRole("button", { name: "Pause background video" })).toBeNull();
    await screen.findByLabelText("Synthèses de l’activité filtrée");
  });
  it("protects homepage statistics until a session exists", async () => {
    window.history.replaceState(null, "", "/"); const store = createAuthStore({ tokenSink: vi.fn() });
    render(<App authStore={store} />); await screen.findByRole("heading", { name: "Connexion" });
    expect(screen.queryByRole("heading", { name: "Dashboard" })).toBeNull();
    expect(screen.queryByTestId("hero-media")).toBeNull(); expect(store.getAccessToken()).toBeNull();
  });
  it("returns to the homepage after login, keeps the skip link local and protects logout", async () => {
    window.history.replaceState(null, "", "/");
    const store = createAuthStore({ tokenSink: vi.fn(), service: { signIn: async () => ({ token: "test", expiresAt: Date.now() + 60000, user: { displayName: "Compte de test" } }) } });
    render(<App authStore={store} />); await screen.findByRole("heading", { name: "Connexion" });
    await act(async () => { await store.login({}); });
    await screen.findByRole("heading", { name: "Dashboard" }); expect(window.location.hash).toBe("#home");
    fireEvent.click(screen.getByRole("link", { name: "Aller au contenu principal" }));
    await vi.waitFor(() => expect(window.location.hash).toBe("#home-content"));
    expect(screen.getByRole("heading", { name: "Dashboard" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Se déconnecter" }));
    await screen.findByRole("heading", { name: "Connexion" }); expect(screen.queryByRole("heading", { name: "Dashboard" })).toBeNull();
  });
});
