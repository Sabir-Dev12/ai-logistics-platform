import { fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import AgentPage from "../src/Agent/AgentPage";

afterEach(() => vi.unstubAllGlobals());
describe("mock AI agent page", () => {
  it("shows a grounded mock conversation, ordered trace, sources, tools and response duration without network calls", () => {
    const fetch = vi.fn(); vi.stubGlobal("fetch", fetch); render(<AgentPage />);
    expect(screen.getByRole("log", { name: "Conversation" })).toBeTruthy();
    expect(screen.getByRole("article", { name: "Message de Vous" }).textContent).toContain("SHP-DEMO-001");
    expect(screen.getByRole("article", { name: "Message de Assistant" }).textContent).toContain("GetShipment");
    const trace = within(screen.getByRole("list", { name: "Étapes d’exécution" }));
    expect(trace.getAllByRole("listitem").map((item) => item.textContent)).toEqual([
      expect.stringContaining("Question utilisateur"), expect.stringContaining("Agent — analyse"), expect.stringContaining("RAG Search"),
      expect.stringContaining("MCP GetShipment"), expect.stringContaining("LLM"), expect.stringContaining("Réponse"),
    ]);
    expect(screen.getByText("Temps de réponse : 1,24 s")).toBeTruthy();
    expect(screen.getByRole("table", { name: "Sources RAG utilisées" })).toBeTruthy();
    expect(fetch).not.toHaveBeenCalled();
  });
  it("opens the cited passage and structured MCP result and closes each modal", () => {
    render(<AgentPage />);
    fireEvent.click(screen.getByRole("button", { name: "Lire Guide des expéditions.pdf" }));
    expect(screen.getByRole("dialog").textContent).toContain("contacter le service logistique");
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Fermer", exact: true }));
    fireEvent.click(screen.getByRole("button", { name: "Détails GetShipment" }));
    const dialog = within(screen.getByRole("dialog"));
    expect(dialog.getByRole("region", { name: "Paramètres" }).textContent).toContain('"shipmentId": "SHP-DEMO-001"');
    expect(dialog.getByRole("region", { name: "Résultat" }).textContent).toContain('"status": "En transit"');
    expect(dialog.queryByRole("textbox")).toBeNull();
    fireEvent.click(dialog.getByRole("button", { name: "Fermer", exact: true })); expect(screen.queryByRole("dialog")).toBeNull();
  });
  it("shows a pending response and only the current running step", () => {
    render(<AgentPage />); fireEvent.change(screen.getByRole("combobox"), { target: { value: "running" } });
    expect(screen.getByRole("status", { name: "Agent en cours — attente de GetShipment…" })).toBeTruthy();
    expect(screen.queryByRole("article", { name: "Message de Assistant" })).toBeNull();
    const steps = screen.getAllByRole("listitem");
    expect(steps.filter((item) => item.getAttribute("aria-current") === "step")).toHaveLength(1);
    expect(steps[4].textContent).toContain("En attente");
    fireEvent.click(screen.getByRole("button", { name: "Détails GetShipment" }));
    expect(screen.getByText("Résultat en attente")).toBeTruthy();
  });
  it("shows tool failure, skipped LLM/response and excludes successful business data from the error response", () => {
    render(<AgentPage />); fireEvent.change(screen.getByRole("combobox"), { target: { value: "failed" } });
    expect(screen.getByRole("alert").textContent).toContain("tool_call_failed");
    expect(screen.getByRole("article", { name: "Message de Assistant" }).textContent).not.toContain("En transit");
    expect(screen.getAllByRole("listitem")[4].textContent).toContain("Non exécuté");
    expect(screen.getByText("Temps de réponse : 0,82 s")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Détails GetShipment" }));
    expect(within(screen.getByRole("dialog")).getByRole("alert").textContent).toContain("shipment_unavailable");
    expect(screen.getByText("Aucun résultat disponible")).toBeTruthy();
  });
});
