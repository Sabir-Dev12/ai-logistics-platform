import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import McpToolsPage from "../src/McpTools/McpToolsPage";
import { createMockToolRepository } from "../src/McpTools/mockTools";

beforeEach(() => vi.useFakeTimers());
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });
const settle = async () => { await act(async () => { await vi.advanceTimersByTimeAsync(250); }); };

describe("MCP tools consultation", () => {
  it("loads the five tools and exposes success details without network calls or execution controls", async () => {
    const fetch = vi.fn(); vi.stubGlobal("fetch", fetch);
    render(<McpToolsPage />);
    expect(screen.getByRole("status", { name: "Chargement…" })).toBeTruthy();
    await settle();
    expect(screen.getByText("5 tools affichés")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Voir GetClient" }));
    const dialog = within(screen.getByRole("dialog"));
    expect(dialog.getByRole("table", { name: "Paramètres attendus" })).toBeTruthy();
    expect(dialog.getByText("clientId", { exact: true })).toBeTruthy();
    expect(dialog.getByText(/Client de démonstration/)).toBeTruthy();
    expect(dialog.getByText("Aucune erreur enregistrée.")).toBeTruthy();
    expect(dialog.queryByRole("textbox")).toBeNull();
    expect(dialog.queryByRole("button", { name: /exécuter/i })).toBeNull();
    expect(fetch).not.toHaveBeenCalled();
    fireEvent.click(dialog.getByRole("button", { name: "Fermer", exact: true }));
    expect(screen.queryByRole("dialog")).toBeNull();
  });
  it("combines accent-insensitive search, category and status filters and resets them", async () => {
    render(<McpToolsPage />); await settle();
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "EXPEDITION" } });
    expect(screen.getByText("2 tools affichés")).toBeTruthy();
    fireEvent.change(screen.getByRole("combobox", { name: "Catégorie" }), { target: { value: "Suivi" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Statut" }), { target: { value: "unavailable" } });
    expect(screen.getByText("GetTracking")).toBeTruthy();
    expect(screen.getByText("1 tool affiché")).toBeTruthy();
    fireEvent.change(screen.getByRole("combobox", { name: "Statut" }), { target: { value: "available" } });
    expect(screen.getByText("Aucun résultat")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Réinitialiser les filtres" }));
    expect(screen.getByText("5 tools affichés")).toBeTruthy();
  });
  it("shows failed and unavailable tool errors and the never-used empty state", async () => {
    render(<McpToolsPage />); await settle();
    for (const [name, code] of [["GetInvoice", "invoice_not_found"], ["GetTracking", "tracking_unavailable"]]) {
      fireEvent.click(screen.getByRole("button", { name: `Voir ${name}` }));
      const dialog = within(screen.getByRole("dialog"));
      expect(dialog.getByRole("alert").textContent).toContain(code);
      expect(dialog.getByText("Aucun résultat disponible")).toBeTruthy();
      fireEvent.click(dialog.getByRole("button", { name: "Fermer", exact: true }));
    }
    fireEvent.click(screen.getByRole("button", { name: "Voir GetBooking" }));
    const dialog = within(screen.getByRole("dialog"));
    expect(dialog.getByText("Aucun appel enregistré")).toBeTruthy();
    expect(dialog.getByText("Dernière utilisation : Jamais utilisé")).toBeTruthy();
  });
  it("renders an empty catalogue and recovers from a load error", async () => {
    const view = render(<McpToolsPage repository={createMockToolRepository({ tools: [] })} />); await settle();
    expect(screen.getByText("Aucun tool")).toBeTruthy(); view.unmount();
    render(<McpToolsPage repository={createMockToolRepository({ failListOnce: true })} />); await settle();
    expect(screen.getByRole("alert").textContent).toContain("Impossible de charger les tools");
    fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await settle();
    expect(screen.getByText("5 tools affichés")).toBeTruthy();
  });
  it("aborts catalogue loading when leaving the page", async () => {
    let signal;
    const list = vi.fn((options) => { signal = options.signal; return new Promise(() => {}); });
    const view = render(<McpToolsPage repository={{ list }} />);
    await act(async () => {});
    view.unmount(); expect(signal.aborted).toBe(true);
  });
});
