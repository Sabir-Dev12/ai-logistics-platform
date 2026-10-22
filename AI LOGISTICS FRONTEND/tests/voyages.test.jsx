import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import VoyagesPage from "../src/Voyages/VoyagesPage";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
import { createMockVoyageRepository, voyageSeed } from "../src/Voyages/mockVoyageRepository";
import { previewCosts } from "../src/Voyages/voyageRules";
const dialog = () => screen.getByRole("dialog");
const click = (name) => fireEvent.click(within(dialog()).getByRole("button", { name, exact: true }));
const field = (label, value) => fireEvent.change(within(dialog()).getByLabelText(label, { exact: true }), { target: { value } });
async function ready(repo = createMockVoyageRepository({ delay: 0 })) { render(<VoyagesPage repository={repo} />); await screen.findByRole("table", { name: "Liste des voyages" }); return repo; }
async function open(number = "VOY-DEMO-001") { fireEvent.click(screen.getByRole("button", { name: number, exact: true })); await screen.findByRole("dialog", { name: "Fiche voyage" }); }
describe("Voyages", () => {
  it("filters, paginates and resets the voyage list", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Suivant", exact: true })); expect(screen.getByText("6–7 sur 7 voyages")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Statut du voyage"), { target: { value: "Clôturé" } }); expect(screen.getByText("1–1 sur 1 voyages")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Rechercher un voyage"), { target: { value: "introuvable" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy();
    fireEvent.click(screen.getAllByRole("button", { name: "Réinitialiser les filtres", exact: true })[0]); expect(screen.getByText("1–5 sur 7 voyages")).toBeTruthy();
  });
  it("creates a draft and persists header edits through refresh", async () => {
    const repo = await ready(); fireEvent.click(screen.getByRole("button", { name: "Créer un voyage", exact: true })); click("Enregistrer le voyage"); expect(within(dialog()).getByText("Le numéro de voyage est obligatoire.")).toBeTruthy();
    field("Numéro de voyage", "VOY-TEST"); field("Destination", "Agadir"); click("Enregistrer le voyage"); await screen.findByRole("dialog", { name: "Fiche voyage" });
    click("Modifier le voyage"); field("Commentaire", "Voyage modifié"); click("Enregistrer le voyage"); await screen.findByRole("dialog", { name: "Fiche voyage" }); expect(within(dialog()).getByText("Voyage modifié")).toBeTruthy();
    click("Fermer"); fireEvent.click(screen.getByRole("button", { name: "Actualiser", exact: true })); await screen.findByRole("table", { name: "Liste des voyages" }); expect((await repo.list()).voyages[0].dateVoyage).toBeNull();
  });
  it("validates, refuses uninvoiced closure, closes after detach and reopens", async () => {
    const repo = await ready(); await open(); click("Valider le voyage"); click("Confirmer"); await screen.findByRole("dialog", { name: "Fiche voyage" });
    click("Clôturer le voyage"); click("Confirmer"); await within(dialog()).findByText("Clôture impossible : un dossier associé est non facturé."); click("Annuler");
    click("Dossiers associés"); click("Détacher DOS-DEMO-001"); await waitFor(() => expect(within(dialog()).queryByText("DOS-DEMO-001")).toBeNull()); click("Résumé"); click("Clôturer le voyage"); click("Confirmer"); await screen.findByRole("dialog", { name: "Fiche voyage" });
    expect(within(dialog()).getByRole("button", { name: "Modifier le voyage", exact: true }).disabled).toBe(true); click("Rouvrir le voyage"); click("Confirmer"); await screen.findByRole("dialog", { name: "Fiche voyage" }); expect((await repo.list()).voyages[0].dateCloture).toBeNull();
  });
  it("edits, adds and removes trajets with validation", async () => {
    const repo = await ready(); await open(); click("Trajets"); click("Gérer les trajets"); field("Kilométrage — trajet 1", "-1"); click("Enregistrer les trajets"); await within(dialog()).findByText("Le kilométrage doit être positif ou nul.");
    field("Kilométrage — trajet 1", "500"); click("Ajouter un trajet"); field("Provenance — trajet 2", "Tanger"); field("Destination — trajet 2", "Rabat"); click("Retirer le trajet 1"); click("Enregistrer les trajets"); await screen.findByRole("dialog", { name: "Fiche voyage" }); expect((await repo.list()).voyages[0].trajets[0].destination).toBe("Rabat");
  });
  it("reuses the equipment editor and enforces integer quantities", async () => {
    const repo = await ready(); await open(); click("Équipements"); click("Gérer les équipements"); field("Quantité — ligne 1", "1.5"); click("Enregistrer les lignes"); await within(dialog()).findByText(/quantité entière/);
    field("Quantité — ligne 1", "2"); click("Enregistrer les lignes"); await screen.findByRole("dialog", { name: "Fiche voyage" }); expect((await repo.list()).voyages[0].equipements[0].qte).toBe(2);
  });
  it("associates and detaches a free dossier", async () => {
    const repo = await ready(); await open(); click("Dossiers associés"); field("Dossier à associer", "DOS-DEMO-7"); click("Associer le dossier"); await waitFor(() => expect((within(dialog()).getByRole("table", { name: "Dossiers associés au voyage" })).textContent).toContain("DOS-DEMO-007"));
    click("Détacher DOS-DEMO-007"); await waitFor(() => expect(within(dialog()).queryByRole("button", { name: "Détacher DOS-DEMO-007" })).toBeNull()); expect((await repo.list()).dossiers.find((item) => item.keyDossier === "DOS-DEMO-7").keyVoyageInter).toBeNull();
  });
  it("shows rates and preview, confirms generation, and prevents duplicate costs", async () => {
    const repo = await ready(); await open("VOY-DEMO-002"); click("Taux et coûts"); expect(within(dialog()).getByRole("table", { name: "Taux de coûts fictifs" })).toBeTruthy(); expect(within(dialog()).getByRole("table", { name: "Aperçu des coûts à générer" })).toBeTruthy();
    const before = (await repo.list()).voyages[1]; expect(before.generatedCosts).toHaveLength(0);
    click("Générer les coûts (démo)"); click("Confirmer"); await screen.findByRole("dialog", { name: "Fiche voyage" }); expect(within(dialog()).getByText("Aucun nouveau coût à générer")).toBeTruthy(); expect(within(dialog()).getByRole("button", { name: "Générer les coûts (démo)", exact: true }).disabled).toBe(true);
    const row = (await repo.list()).voyages[1]; expect(row.generatedCosts.length).toBeGreaterThan(0); await expect(repo.generate(row.id, { month: row.dateVoyage.slice(0, 7) })).rejects.toThrow("déjà été générées");
  });
  it("explains invalid month and missing validation or dossiers", async () => {
    const seed = voyageSeed(), row = seed.voyages[0]; expect(previewCosts(row, seed.dossiers, "2026-10").reason).toContain("validé");
    row.dateVoyage = "2026-10-01"; expect(previewCosts(row, seed.dossiers, "2026-09").eligible).toBe(false); expect(previewCosts(row, [], "2026-10").reason).toContain("Aucun dossier");
    await ready(); await open(); click("Taux et coûts"); expect(within(dialog()).getByText("Le voyage doit être validé.")).toBeTruthy();
  });
  it("shows loading, error, retry and an empty list", async () => {
    const seed = voyageSeed(); seed.voyages = []; const repo = createMockVoyageRepository({ seed, delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list);
    render(<VoyagesPage repository={{ ...repo, list }} />); expect(screen.getByRole("status", { name: "Chargement des voyages…" })).toBeTruthy(); await screen.findByText("Impossible de charger les voyages. Réessayez."); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await screen.findByText("Aucun voyage"); expect(screen.queryByText("private")).toBeNull();
  });
  it("keeps failed mutations open and guards closed voyages", async () => {
    const repo = createMockVoyageRepository({ delay: 0 }); await ready({ ...repo, update: vi.fn().mockRejectedValue(new Error("private")) }); await open(); click("Modifier le voyage"); click("Enregistrer le voyage"); await within(dialog()).findByText("L’opération n’a pas pu aboutir. Réessayez.");
    await expect(repo.assign("VOY-DEMO-3", { dossierId: "DOS-DEMO-7" })).rejects.toThrow("clôturé"); await expect(repo.setTrajets("VOY-DEMO-3", [])).rejects.toThrow("clôturé");
  });
  it("protects the voyage route and restores it after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#voyages");
    const store = createAuthStore({ tokenSink: vi.fn(), service: { signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Test" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Voyages", exact: true }); expect(window.location.hash).toBe("#voyages"); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
