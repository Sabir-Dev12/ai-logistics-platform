import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";

vi.mock("../src/Dashboard/DashboardPage", () => ({ default: () => <p>Dashboard existant</p> }));
beforeEach(() => {
  vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
  window.history.replaceState(null, "", "/#chat");
});
afterEach(() => { window.history.replaceState(null, "", "/"); vi.unstubAllGlobals(); });

it("opens the chat through navigation and the skip link preserves the selected page", async () => {
  const store = createAuthStore({ tokenSink: () => {}, service: { signIn: async () => ({ token: "test", expiresAt: Date.now() + 60000, user: { displayName: "Test" } }) } });
  await store.login({});
  render(<App authStore={store} />);
  expect(screen.getByRole("heading", { name: "Assistant logistique" })).toBeTruthy();
  expect(screen.getByRole("link", { name: "AI Chatbot" }).getAttribute("aria-current")).toBe("page");
  fireEvent.click(screen.getByRole("link", { name: "Aller au contenu principal" }));
  expect(document.activeElement).toBe(screen.getByRole("main"));
  expect(window.location.hash).toBe("#chat");
  act(() => { window.history.replaceState(null, "", "/#main-content"); window.dispatchEvent(new HashChangeEvent("hashchange")); });
  expect(screen.getByText("Dashboard existant")).toBeTruthy();
  expect(screen.queryByRole("heading", { name: "Assistant logistique" })).toBeNull();
  act(() => store.logout());
});
