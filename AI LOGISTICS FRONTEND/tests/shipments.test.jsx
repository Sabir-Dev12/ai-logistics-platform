import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ShipmentsPage from "../src/Shipments/ShipmentsPage";
import { createMockShipmentRepository, shipmentSeed } from "../src/Shipments/mockShipmentRepository";
import { splitByTaxableWeight } from "../src/Shipments/shipmentRules";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
const ready = async (repository = createMockShipmentRepository({ delay: 0 })) => { render(<ShipmentsPage repository={repository} />); await screen.findByRole("table", { name: "Liste des dossiers" }); return repository; };
const dialog = () => screen.getByRole("dialog");
const openFirst = async () => { fireEvent.click(screen.getByRole("button", { name: "DOS-DEMO-001", exact: true })); await screen.findByRole("dialog", { name: "Fiche dossier" }); };
const field = (label, value) => fireEvent.change(within(dialog()).getByLabelText(label, { exact: true }), { target: { value } });
const click = (name) => fireEvent.click(within(dialog()).getByRole("button", { name, exact: true }));
describe("Dossiers / Expéditions", () => {
  it("protects the new route and restores it after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
    window.history.replaceState(null, "", "#shipments");
    const store = createAuthStore({ tokenSink: vi.fn(), service: { signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte fictif" } }) } });
    const view = render(<App authStore={store} />);
    try {
      await screen.findByRole("heading", { name: "Connexion" }); expect(screen.queryByRole("heading", { name: "Dossiers / Expéditions" })).toBeNull();
      await act(async () => { await store.login({}); });
      await screen.findByRole("heading", { name: "Dossiers / Expéditions" }); expect(window.location.hash).toBe("#shipments");
      expect(screen.getByRole("link", { name: "Dossiers / Expéditions", exact: true }).getAttribute("aria-current")).toBe("page");
    } finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
  it("combines all filters, resets and paginates", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Suivant", exact: true })); expect(screen.getByText("6–8 sur 8 dossiers")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Client", { exact: true }), { target: { value: "CLI-DEMO-1" } });
    fireEvent.change(screen.getByLabelText("Voyage", { exact: true }), { target: { value: "VOY-DEMO-1" } });
    fireEvent.change(screen.getByLabelText("Type", { exact: true }), { target: { value: "RI" } });
    fireEvent.change(screen.getByLabelText("Statut de facturation"), { target: { value: "NonFacture" } });
    expect(screen.getByText("1–1 sur 1 dossiers")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Rechercher un dossier"), { target: { value: "inconnu" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy();
    fireEvent.click(screen.getAllByRole("button", { name: "Réinitialiser les filtres", exact: true })[0]); expect(screen.getByText("1–5 sur 8 dossiers")).toBeTruthy();
  });
  it("creates, validates and edits a dossier, persisting in memory on refresh", async () => {
    const repo = await ready(); fireEvent.click(screen.getByRole("button", { name: "Créer un dossier", exact: true })); click("Enregistrer le dossier"); expect(within(dialog()).getByText("Le numéro de dossier est obligatoire.")).toBeTruthy();
    field("Numéro de dossier", "DOS-TEST"); field("Client du dossier", "CLI-DEMO-1"); field("Poids taxable (kg)", "450"); click("Enregistrer le dossier");
    await screen.findByRole("dialog", { name: "Fiche dossier" }); expect(within(dialog()).getByRole("heading", { name: "DOS-TEST" })).toBeTruthy();
    click("Modifier le dossier"); field("Description de la marchandise", "Chargement modifié"); click("Enregistrer le dossier"); await screen.findByRole("dialog", { name: "Fiche dossier" });
    expect(within(dialog()).getByText("Chargement modifié")).toBeTruthy(); click("Fermer"); fireEvent.click(screen.getByRole("button", { name: "Actualiser", exact: true })); await screen.findByRole("table", { name: "Liste des dossiers" });
    expect((await repo.list()).shipments.find((row) => row.numDossier === "DOS-TEST").poidsTaxable).toBe(450);
  });
  it("edits, adds and removes packing lines without overwriting header totals", async () => {
    const repo = await ready(); await openFirst(); click("Gérer les colisages"); field("Nombre de colis — ligne 1", "4"); click("Ajouter un colisage"); field("Type de colis — ligne 2", "Carton"); click("Enregistrer les colisages");
    await screen.findByRole("dialog", { name: "Fiche dossier" }); expect(within(dialog()).getByText("Carton")).toBeTruthy(); click("Gérer les colisages"); click("Ajouter un colisage"); field("Type de colis — ligne 3", "Caisse"); click("Retirer le colisage 1"); click("Enregistrer les colisages");
    await screen.findByRole("dialog", { name: "Fiche dossier" }); const row = (await repo.list()).shipments[0]; expect(row.colisages.map((line) => line.typeColis)).toEqual(["Carton", "Caisse"]); expect(new Set(row.colisages.map((line) => line.id)).size).toBe(2); expect(row.nbColis).toBe(10);
  });
  it("records and cancels MEAD depotage", async () => {
    const repo = await ready(); await openFirst(); click("Enregistrer le dépotage"); field("Colis dépotés", "5"); field("Référence de dépotage", "DEPOT-TEST"); click("Enregistrer le dépotage");
    await screen.findByRole("dialog", { name: "Fiche dossier" }); expect(within(dialog()).getByText("Référence : DEPOT-TEST")).toBeTruthy(); click("Annuler le dépotage"); click("Confirmer"); await screen.findByRole("dialog", { name: "Fiche dossier" });
    const row = (await repo.list()).shipments[0]; expect(row.dateDepotage).toBeNull(); expect(row.colisDepote).toBeNull(); expect(row.colisSortie).toBe(0); expect(row.refDepotage).toBe("");
  });
  it("blocks invoiced deletion and depotage outside MEAD, and deletes eligible rows", async () => {
    const repo = await ready(); expect(screen.getByRole("button", { name: "Supprimer DOS-DEMO-002" }).disabled).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "DOS-DEMO-002", exact: true })); expect(within(dialog()).getByRole("button", { name: "Enregistrer le dépotage", exact: true }).disabled).toBe(true); click("Fermer");
    await expect(repo.remove("DOS-DEMO-2")).rejects.toThrow("Un dossier facturé"); await expect(repo.unload("DOS-DEMO-2", {})).rejects.toThrow("MEAD");
    fireEvent.click(screen.getByRole("button", { name: "Supprimer DOS-DEMO-001" })); click("Confirmer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect((await repo.list()).shipments).toHaveLength(7);
  });
  it("lists voyage dossiers and simulates taxable-weight allocation, including empty voyages", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Dossiers d’un voyage", exact: true })); field("Voyage à consulter", "VOY-DEMO-1"); expect(within(dialog()).getByRole("table", { name: "Répartition simulée par poids taxable" })).toBeTruthy();
    field("Montant à répartir (MAD)", "-1"); expect(within(dialog()).getByText("Renseignez des montants positifs ou nuls.")).toBeTruthy(); field("Voyage à consulter", "VOY-DEMO-3"); expect(within(dialog()).getByText("Aucun dossier sur ce voyage")).toBeTruthy();
  });
  it("shows loading, error, retry and the initial empty state", async () => {
    const seed = shipmentSeed(); seed.shipments = [];
    const repo = createMockShipmentRepository({ seed, delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private details")).mockImplementation(repo.list);
    render(<ShipmentsPage repository={{ ...repo, list }} />); expect(screen.getByRole("status", { name: "Chargement des dossiers…" })).toBeTruthy();
    await screen.findByText("Impossible de charger les dossiers. Réessayez."); expect(screen.queryByText("private details")).toBeNull(); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await screen.findByText("Aucun dossier");
  });
  it("keeps a failed mutation open for correction or retry", async () => {
    const repo = createMockShipmentRepository({ delay: 0 }); await ready({ ...repo, update: vi.fn().mockRejectedValue(new Error("private details")) }); await openFirst(); click("Modifier le dossier"); click("Enregistrer le dossier");
    await within(dialog()).findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(screen.getByRole("dialog", { name: "Modifier le dossier" })).toBeTruthy();
  });
  it("validates duplicate numbers, packing values and zero taxable weight", async () => {
    const repo = createMockShipmentRepository({ delay: 0 }); await expect(repo.create({ numDossier: "DOS-DEMO-001", typeDossier: "RI" })).rejects.toThrow("déjà utilisé");
    await expect(repo.setPacking("DOS-DEMO-1", [{ typeColis: "Palette", nbColis: -1 }])).rejects.toThrow("positives ou nulles");
    expect(splitByTaxableWeight([{ poidsTaxable: 0 }], 100, 10)).toEqual([]);
    expect(splitByTaxableWeight([{ poidsTaxable: 100 }, { poidsTaxable: 300 }], 1000, 100).map((row) => row.montantMad)).toEqual([250, 750]);
  });
});
