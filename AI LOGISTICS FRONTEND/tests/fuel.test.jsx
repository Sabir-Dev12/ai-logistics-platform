import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import FuelPage from "../src/Fuel/FuelPage";
import { createMockFuelRepository, fuelMovements, fuelSeed } from "../src/Fuel/mockFuelRepository";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
const tab = (name) => fireEvent.click(screen.getByRole("button", { name, exact: true }));
const dialog = () => screen.getByRole("dialog");
const click = (name) => fireEvent.click(within(dialog()).getByRole("button", { name, exact: true }));
const field = (label, value) => fireEvent.change(within(dialog()).getByLabelText(label, { exact: true }), { target: { value } });
async function ready(repo = createMockFuelRepository({ delay: 0 })) { render(<FuelPage repository={repo} />); await screen.findByRole("table", { name: "Citernes" }); return repo; }
const movement = (extra = {}) => ({ tankId: "CIT-1", date: "2026-10-17T08:00", quantity: 100, equipmentId: "MAT-DEMO-1", driverId: "", voyage: "", meter: "", full: false, note: "", ...extra });
describe("Carburant", () => {
  it("exposes all eight views, filters and paginates movements", async () => {
    await ready(); tab("Mouvements"); expect(screen.getByText("1–5 sur 6 enregistrements")).toBeTruthy(); tab("Suivant"); expect(screen.getByText("6–6 sur 6 enregistrements")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Produit filtré"), { target: { value: "AdBlue" } }); expect(screen.getByText("1–1 sur 1 enregistrements")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Rechercher dans le carburant"), { target: { value: "introuvable" } }); expect(screen.getByText("Aucune donnée")).toBeTruthy();
    for (const name of ["Réceptions", "Approvisionnements", "Bons", "Sorties", "Jauges", "Ratios", "Citernes"]) { tab(name); expect(screen.getByRole("table", { name, exact: true })).toBeTruthy(); }
  });
  it("creates a tank and receives fuel with recalculated weighted price", async () => {
    const repo = await ready(); tab("Créer une citerne"); field("Libellé de citerne", "Citerne Test"); click("Enregistrer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect((await repo.list()).tanks[0].stock).toBe(0); tab("Réceptions"); tab("Enregistrer une réception"); field("Quantité (L)", "100"); field("Montant réceptionné (MAD)", "1000"); click("Enregistrer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); const row = (await repo.list()).tanks[0]; expect(row.stock).toBe(100); expect(row.pmp).toBe(10);
  });
  it("weights deliveries by existing stock and preserves issue price history", async () => {
    const repo = createMockFuelRepository({ delay: 0 }); await repo.save("reception", { tankId: "CIT-1", quantity: 300, amount: 6000, date: "2026-10-17T08:00" }); const state = await repo.list(); expect(state.tanks[0].stock).toBe(1500); expect(state.tanks[0].pmp).toBe(12); expect(state.issues[0].unitPrice).toBe(10); expect(state.issues[0].amount).toBe(1000);
  });
  it("records multi-tank replenishment with pro-rata amounts and atomic validation", async () => {
    const repo = createMockFuelRepository({ delay: 0 }); const value = { supplierId: "FOU-1", reference: "BL-TEST", date: "2026-10-17T08:00", amount: 4000, lines: [{ tankId: "CIT-1", quantity: 100 }, { tankId: "CIT-2", quantity: 300 }] }; await repo.save("supply", value); let state = await repo.list(); expect(state.supplies[0].lines.map((line) => line.amount)).toEqual([1000, 3000]); expect(state.tanks[0].stock).toBe(1300); expect(state.tanks[1].stock).toBe(800);
    await expect(repo.save("supply", { ...value, lines: [{ tankId: "CIT-1", quantity: 100 }, { tankId: "CIT-2", quantity: -1 }] })).rejects.toThrow("Quantité de ligne"); state = await repo.list(); expect(state.tanks[0].stock).toBe(1300); expect(state.supplies).toHaveLength(1);
  });
  it("edits delivery lines through the reused design system and shows their detail", async () => {
    await ready(); tab("Approvisionnements"); tab("Créer un approvisionnement"); field("Numéro BL", "BL-UI"); field("Montant total du BL (MAD)", "2000"); field("Quantité (L) — ligne 1", "100"); click("Ajouter une ligne"); field("Citerne — ligne 2", "CIT-2"); field("Quantité (L) — ligne 2", "100"); click("Enregistrer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); const reference = screen.getByRole("table", { name: "Approvisionnements" }).querySelector("tbody button"); fireEvent.click(reference); expect(within(dialog()).getByRole("table", { name: "Répartition par citerne" })).toBeTruthy(); expect(within(dialog()).getByText("Ce document n’est pas annulable.")).toBeTruthy();
  });
  it("records supplier vouchers without changing tank stocks or duplicating journal entries", async () => {
    const repo = createMockFuelRepository({ delay: 0 }); await repo.save("voucher", movement({ supplierId: "FOU-1", reference: "BON-1", amount: 123.45, currency: "EUR", product: "Gasoil", quantity: 10 })); const state = await repo.list(); expect(state.tanks[0].stock).toBe(1200); expect(state.vouchers[0].amount).toBe(123.45); expect(fuelMovements(state).filter((row) => row.kind === "Bon fournisseur")).toHaveLength(1);
  });
  it("issues at PMP, rejects negative stock and cancels exactly once", async () => {
    const repo = createMockFuelRepository({ delay: 0 }); await expect(repo.save("issue", movement({ quantity: 1300 }))).rejects.toThrow("Stock insuffisant"); await repo.save("issue", movement()); let state = await repo.list(); const issue = state.issues[0]; expect(state.tanks[0].stock).toBe(1100); expect(issue.amount).toBe(1000); await repo.save("cancel", { id: issue.id }); await repo.save("cancel", { id: issue.id }); state = await repo.list(); expect(state.tanks[0].stock).toBe(1200); expect(state.issues[0].cancelled).toBe(true); expect(fuelMovements(state).find((row) => row.id === issue.id).delta).toBe(0);
  });
  it("uses the issue form and confirms cancellation from its detail", async () => {
    const repo = await ready(); tab("Sorties"); tab("Enregistrer une sortie"); field("Quantité (L)", "50"); click("Enregistrer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); fireEvent.click(screen.getByRole("table", { name: "Sorties" }).querySelector("tbody button")); click("Annuler la sortie"); click("Confirmer l’annulation"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect((await repo.list()).tanks[0].stock).toBe(1200);
  });
  it("snapshots a gauge and leaves theoretical stock unchanged", async () => {
    const repo = await ready(); tab("Jauges"); tab("Saisir une jauge"); field("Quantité réelle (L)", "1150"); click("Enregistrer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); const state = await repo.list(); expect(state.tanks[0].stock).toBe(1200); expect(state.gauges[0]).toMatchObject({ actual: 1150, theoretical: 1200, gap: 50 }); await repo.save("reception", { tankId: "CIT-1", quantity: 100, amount: 1000, date: "2026-10-17T09:00" }); expect((await repo.list()).gauges[0].theoretical).toBe(1200);
  });
  it("calculates full-to-full ratios including top-ups and chains the closing fill", async () => {
    const repo = createMockFuelRepository({ delay: 0 }); await repo.save("ratio", { equipmentId: "MAT-DEMO-1", product: "Gasoil" }); let state = await repo.list(); expect(state.ratios[0]).toMatchObject({ distance: 1000, litres: 200, amount: 2000, consumption: 20, costKm: 2 }); expect(state.issues.find((row) => row.id === "SOR-3").calculated).toBe(false); await expect(repo.save("cancel", { id: "SOR-3" })).rejects.toThrow("ratio calculé"); const missing = await repo.save("ratio", { equipmentId: "MAT-DEMO-1", product: "Gasoil" }); expect(missing.notice).toContain("deux pleins"); expect(missing.ratios).toHaveLength(1);
    await repo.save("issue", movement({ date: "2026-10-17T08:00", quantity: 100, meter: 12000, full: true })); await repo.save("ratio", { equipmentId: "MAT-DEMO-1", product: "Gasoil" }); state = await repo.list(); expect(state.ratios).toHaveLength(2); expect(state.ratios[0].litres).toBe(100); expect(state.ratios[0].from).toBe("2026-10-04T08:00");
  });
  it("does not invent ratios when currency, distance, cost or full tanks are unsuitable", async () => {
    const seed = fuelSeed(); seed.issues[1].currency = "EUR"; const repo = createMockFuelRepository({ seed, delay: 0 }); await expect(repo.save("ratio", { equipmentId: "MAT-DEMO-1", product: "Gasoil" })).rejects.toThrow("plusieurs devises"); expect((await repo.list()).ratios).toHaveLength(0); const empty = createMockFuelRepository({ delay: 0 }); expect((await empty.save("ratio", { equipmentId: "MAT-DEMO-2", product: "Gasoil" })).notice).toContain("deux pleins");
  });
  it("renders ratio results and date-filter errors", async () => {
    await ready(); tab("Ratios"); tab("Calculer un ratio"); field("Matériel", "MAT-DEMO-1"); click("Calculer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect(screen.getByRole("table", { name: "Ratios" }).textContent).toContain("20"); fireEvent.change(screen.getByLabelText("Du", { exact: true }), { target: { value: "2026-10-17" } }); fireEvent.change(screen.getByLabelText("Au", { exact: true }), { target: { value: "2026-01-01" } }); expect(screen.getByText("La date de fin doit être postérieure ou égale à la date de début.")).toBeTruthy();
  });
  it("covers loading, load failure, retry, empty and retained failed form values", async () => {
    const seed = fuelSeed(); seed.tanks = []; const repo = createMockFuelRepository({ seed, delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list); render(<FuelPage repository={{ ...repo, list, save: vi.fn().mockRejectedValue(new Error("private")) }} />); expect(screen.getByRole("status", { name: "Chargement du carburant…" })).toBeTruthy(); await screen.findByText("Impossible de charger le carburant. Réessayez."); tab("Réessayer"); await screen.findByText("Aucune donnée"); tab("Créer une citerne"); field("Libellé de citerne", "Test"); click("Enregistrer"); await within(dialog()).findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(dialog()).getByLabelText("Libellé de citerne").value).toBe("Test");
  });
  it("aborts a reception without changing stock", async () => {
    const repo = createMockFuelRepository({ delay: 10 }), controller = new AbortController(); const pending = repo.save("reception", { tankId: "CIT-1", quantity: 100, amount: 1000, date: "2026-10-17T08:00" }, { signal: controller.signal }); controller.abort(); await expect(pending).rejects.toMatchObject({ name: "AbortError" }); expect((await repo.list()).tanks[0].stock).toBe(1200);
  });
  it("protects the route and returns to fuel after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#fuel"); const store = createAuthStore({ tokenSink: vi.fn(), service: { authorizedRequest: async () => new Response(JSON.stringify({items:[],page:1,pageSize:5,totalCount:0}),{status:200}), signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte Démo" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Carburant", exact: true }); expect(window.location.hash).toBe("#fuel"); await waitFor(() => expect(screen.getByRole("button", { name: "Créer une citerne", exact: true }).disabled).toBe(false)); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
