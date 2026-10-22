import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import PurchaseScalesPage from "../src/PurchaseScales/PurchaseScalesPage";
import { createMockScaleRepository, scaleSeed } from "../src/PurchaseScales/mockScaleRepository";
import { resolveTariff, validateLines } from "../src/PurchaseScales/scaleRules";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
const dialog = () => screen.getByRole("dialog");
const click = (name) => fireEvent.click(within(dialog()).getByRole("button", { name, exact: true }));
const field = (label, value) => fireEvent.change(within(dialog()).getByLabelText(label, { exact: true }), { target: { value } });
async function ready(repository = createMockScaleRepository({ delay: 0 })) { render(<PurchaseScalesPage repository={repository} />); await screen.findByRole("table", { name: "Liste des barèmes d’achat" }); return repository; }
async function open(id = "BA-DEMO-1") { fireEvent.click(screen.getByRole("button", { name: id, exact: true })); await detail(); }
async function detail() { await screen.findByRole("dialog", { name: "Fiche barème d’achat" }); }
describe("Barèmes d’achat", () => {
  it("searches supplier and accents, filters status/type, paginates and resets", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Suivant", exact: true })); expect(screen.getByText("6–7 sur 7 barèmes")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Statut du barème"), { target: { value: "Non validé" } }); expect(screen.getByText("1–1 sur 1 barème")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Type de barème"), { target: { value: "Groupage" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy();
    fireEvent.click(screen.getAllByRole("button", { name: "Réinitialiser les filtres" })[0]); fireEvent.change(screen.getByLabelText("Rechercher un barème"), { target: { value: "atlas transit demo" } }); expect(screen.getByText("1–4 sur 4 barèmes")).toBeTruthy();
  });
  it("creates initial columns, validates and modifies the header", async () => {
    const repo = await ready(); fireEvent.click(screen.getByRole("button", { name: "Créer un barème", exact: true })); click("Enregistrer le barème"); expect(within(dialog()).getByText("Le libellé du barème est obligatoire.")).toBeTruthy();
    field("Libellé", "Barème TEST"); field("Début de validité", "2026-10-16"); field("Fin de validité", "2026-01-01"); click("Enregistrer le barème"); expect(within(dialog()).getByText("La fin de validité ne peut pas précéder le début.")).toBeTruthy(); field("Fin de validité", "2026-12-31"); click("Enregistrer le barème"); await detail();
    expect(within(dialog()).getByRole("button", { name: "Modifier le barème", exact: true }).disabled).toBe(true); click("Valider le barème"); click("Confirmer"); await detail(); click("Modifier le barème"); field("Fournisseur", "Fournisseur TEST"); click("Enregistrer le barème"); await detail(); expect(within(dialog()).getByLabelText("Fournisseur").value).toBe("Fournisseur TEST"); expect((await repo.list()).scales[0].status).toBe("Validé");
  });
  it("edits columns, prevents duplicates, and removes their cells", async () => {
    const repo = await ready(); await open(); click("Colonnes"); click("Gérer les colonnes"); field("Numéro de colonne — ligne 2", "1"); click("Enregistrer les lignes"); expect(within(dialog()).getByText("Les numéros de colonnes doivent être uniques.")).toBeTruthy();
    field("Numéro de colonne — ligne 2", "2"); field("Fin de tranche (exclue) — ligne 1", "50"); click("Retirer la ligne 2"); click("Enregistrer les lignes"); await detail(); const row = (await repo.list()).scales[0]; expect(row.columns).toHaveLength(1); expect(row.columns[0].to).toBe(50); expect(row.cells.every((cell) => cell.columnId === 1)).toBe(true);
  });
  it("adds and edits tariff cells, preserving zero as a valid amount", async () => {
    const repo = await ready(); await open(); click("Cellules tarifaires"); click("Gérer les cellules tarifaires"); click("Ajouter une ligne"); field("Département — ligne 4", "33"); field("Montant — ligne 4", "0"); click("Enregistrer les lignes"); await detail(); click("Recherche de tarif"); field("Département recherché", "33"); field("Valeur à tarifer", "0"); click("Rechercher le tarif"); expect(within(dialog()).getByText("Tarif trouvé")).toBeTruthy(); expect((await repo.list()).scales[0].cells[3].amount).toBe(0);
  });
  it("adds, edits and clears prestations", async () => {
    const repo = await ready(); await open(); click("Prestations"); click("Gérer les prestations"); field("Libellé de prestation — ligne 1", "Livraison"); click("Ajouter une ligne"); field("Code rubrique — ligne 2", "FRET"); field("Montant — ligne 2", "99"); click("Enregistrer les lignes"); await detail(); expect((await repo.list()).scales[0].services).toHaveLength(2);
    click("Prestations"); click("Gérer les prestations"); click("Retirer la ligne 2"); click("Retirer la ligne 1"); click("Enregistrer les lignes"); await detail(); click("Prestations"); expect(within(dialog()).getByText("Aucune prestation")).toBeTruthy();
  });
  it("duplicates all lines with independent identifiers and deletes via confirmation", async () => {
    const repo = await ready(); await open(); click("Dupliquer le barème"); field("Libellé de la copie", "Copie TEST"); click("Confirmer"); await detail();
    const state = await repo.list(), copy = state.scales[0], source = state.scales.find((row) => row.id === "BA-DEMO-1"); expect(copy.status).toBe("Non validé"); expect(copy.cells).toHaveLength(source.cells.length); expect(copy.services[0].id).not.toBe(source.services[0].id);
    click("Supprimer le barème"); click("Annuler"); expect((await repo.list()).scales[0].status).toBe("Non validé"); click("Supprimer le barème"); click("Confirmer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect((await repo.list()).scales[0].status).toBe("Supprimé"); expect(screen.getByText("1–5 sur 7 barèmes")).toBeTruthy();
  });
  it("handles exact boundaries, open bounds, overlap ordering, missing cells and no formula evaluation", () => {
    const row = scaleSeed().scales[0]; expect(resolveTariff(row, "75", 99.99).amount).toBe(150); expect(resolveTariff(row, "75", 100).amount).toBe(250); expect(resolveTariff(row, "13", 100).found).toBe(false); expect(resolveTariff(row, "75", -1).found).toBe(false);
    row.columns[0].from = null; row.columns[0].to = null; row.columns[0].formula = "never execute"; row.columns[0].minimum = 900; row.columns.reverse(); expect(resolveTariff(row, " 75 ", -1).amount).toBe(150); expect(resolveTariff(row, "75", 100).column.columnId).toBe(1);
  });
  it("shows missing tariffs and invalid search values without stale results", async () => {
    await ready(); await open(); click("Recherche de tarif"); click("Rechercher le tarif"); expect(within(dialog()).getByText("Renseignez une valeur numérique.")).toBeTruthy(); field("Département recherché", "75"); field("Valeur à tarifer", "100"); click("Rechercher le tarif"); expect(within(dialog()).getByText("Tarif trouvé")).toBeTruthy(); field("Département recherché", "99"); expect(within(dialog()).queryByText("Tarif trouvé")).toBeNull(); click("Rechercher le tarif"); expect(within(dialog()).getByText("Aucun tarif trouvé")).toBeTruthy();
  });
  it("rejects duplicate labels, invalid column references, ranges and unauthorized draft edits", async () => {
    const repo = createMockScaleRepository({ delay: 0 }); await expect(repo.update("BA-DEMO-2", scaleSeed().scales[1])).rejects.toThrow("validé"); await expect(repo.duplicate("BA-DEMO-1", { label: "TRANSPORT NATIONAL DÉMO 1" })).rejects.toThrow("déjà utilisé");
    await expect(repo.setLines("BA-DEMO-1", { kind: "cells", lines: [{ columnId: 999, department: "75", amount: 2 }] })).rejects.toThrow("existante"); expect(() => validateLines("columns", [{ columnId: 1, from: 100, to: 0 }])).toThrow("fin de tranche");
    expect(() => validateLines("cells", [{ columnId: 1, department: "Ab", amount: 0 }, { columnId: 1, department: "ab", amount: 1 }], [{ columnId: 1 }])).toThrow("seule cellule");
    await repo.setLines("BA-DEMO-1", { kind: "columns", lines: [] }); await expect(repo.validate("BA-DEMO-1")).rejects.toThrow("au moins une colonne");
  });
  it("renders loading, load errors, retry, initial empty and preserves failed forms", async () => {
    const repo = createMockScaleRepository({ seed: { scales: [] }, delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list);
    render(<PurchaseScalesPage repository={{ ...repo, list, create: vi.fn().mockRejectedValue(new Error("private")) }} />); expect(screen.getByRole("status", { name: "Chargement des barèmes…" })).toBeTruthy(); await screen.findByText("Impossible de charger les barèmes. Réessayez."); fireEvent.click(screen.getByRole("button", { name: "Réessayer", exact: true })); await screen.findByText("Aucun barème"); fireEvent.click(screen.getAllByRole("button", { name: "Créer un barème", exact: true })[0]); field("Libellé", "TEST"); click("Enregistrer le barème"); await within(dialog()).findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(dialog()).getByLabelText("Libellé").value).toBe("TEST");
  });
  it("aborts a pending write without changing mock state", async () => {
    const repo = createMockScaleRepository({ delay: 10 }), controller = new AbortController(); const pending = repo.remove("BA-DEMO-1", { signal: controller.signal }); controller.abort(); await expect(pending).rejects.toMatchObject({ name: "AbortError" }); expect((await repo.list()).scales[0].status).toBe("Validé");
  });
  it("protects the new route and returns there after authentication", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#purchase-scales"); const store = createAuthStore({ tokenSink: vi.fn(), service: { signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte Démo" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Barèmes d’achat", exact: true }); expect(window.location.hash).toBe("#purchase-scales"); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
