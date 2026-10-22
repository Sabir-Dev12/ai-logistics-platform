import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import QuotationsPage from "../src/Quotations/QuotationsPage";
import { createMockQuotationRepository, quotationSeed } from "../src/Quotations/mockQuotationRepository";
import { quotationTotals } from "../src/Quotations/quotationRules";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
const dialog = () => screen.getByRole("dialog");
const click = (name) => fireEvent.click(within(dialog()).getByRole("button", { name, exact: true }));
const field = (label, value) => fireEvent.change(within(dialog()).getByLabelText(label, { exact: true }), { target: { value } });
async function ready(repo = createMockQuotationRepository({ delay: 0 })) { render(<QuotationsPage repository={repo} />); await screen.findByRole("table", { name: "Liste des cotations" }); return repo; }
async function open(number = "DEV-DEMO-001") { fireEvent.click(screen.getByRole("button", { name: number, exact: true })); await screen.findByRole("dialog", { name: "Fiche cotation" }); }
async function detail() { await screen.findByRole("dialog", { name: "Fiche cotation" }); }
describe("Cotations / Devis", () => {
  it("filters and paginates quotations", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Suivant", exact: true })); expect(screen.getByText("6–7 sur 7 cotations")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Statut du devis"), { target: { value: "Gagnée" } }); expect(screen.getByText("1–1 sur 1 cotation")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Rechercher un devis"), { target: { value: "introuvable" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy(); fireEvent.click(screen.getAllByRole("button", { name: "Réinitialiser les filtres", exact: true })[0]); expect(screen.getByText("1–5 sur 7 cotations")).toBeTruthy();
  });
  it("creates and modifies a quotation with validation and persistence on refresh", async () => {
    const repo = await ready(); fireEvent.click(screen.getByRole("button", { name: "Créer un devis", exact: true })); click("Enregistrer le devis"); expect(within(dialog()).getByText("Le numéro de cotation et le client sont obligatoires.")).toBeTruthy();
    field("Numéro de cotation", "DEV-TEST"); field("Client du devis", "CLI-DEMO-1"); click("Enregistrer le devis"); await detail(); click("Modifier le devis"); field("Commentaire", "Devis modifié"); click("Enregistrer le devis"); await detail(); expect(within(dialog()).getByText("Devis modifié")).toBeTruthy();
    click("Fermer"); fireEvent.click(screen.getByRole("button", { name: "Actualiser", exact: true })); await screen.findByRole("table", { name: "Liste des cotations" }); expect((await repo.list()).quotations[0].numCotation).toBe("DEV-TEST");
  });
  it("duplicates packing, prices, rates and documents but excludes purchases and groups", async () => {
    const repo = await ready(); await open(); click("Dupliquer le devis"); field("Numéro de la copie", "DEV-COPIE"); click("Confirmer"); await detail();
    const state = await repo.list(), copy = state.quotations[0], source = state.quotations.find((item) => item.id === "COT-DEMO-1"); expect(copy.achats).toEqual([]); expect(copy.groupId).toBe(""); expect(copy.documents).toHaveLength(1); expect(copy.prix[0].id).not.toBe(source.prix[0].id); expect(copy.statut).toBe("Saisie");
    await repo.setLines(copy.id, { kind: "prix", lines: [] }); expect((await repo.list()).quotations.find((item) => item.id === source.id).prix).toHaveLength(1);
  });
  it("simulates sending and refuses sending without price or required groupage packing", async () => {
    const repo = await ready(); await open(); click("Envoyer le devis"); click("Confirmer"); await detail(); expect(within(dialog()).getByText("Envoyée", { exact: true })).toBeTruthy();
    await repo.setLines("COT-DEMO-1", { kind: "prix", lines: [] }); await expect(repo.send("COT-DEMO-1")).rejects.toThrow("ligne de prix");
    await repo.setLines("COT-DEMO-1", { kind: "prix", lines: [{ libelle: "Fret", quantite: 1, typeUtilisation: "Montant", montantValeur: 100, devise: "MAD" }] }); await repo.setLines("COT-DEMO-1", { kind: "colisages", lines: [] }); await expect(repo.send("COT-DEMO-1")).rejects.toThrow("colisage est obligatoire");
  });
  it("closes as lost with a reason and blocks further header changes", async () => {
    const repo = await ready(); await open(); click("Clôturer le devis"); field("Résultat de la cotation", "false"); click("Confirmer"); await within(dialog()).findByText("Le motif est obligatoire pour une cotation perdue."); field("Motif de perte", "Délai"); field("Concurrent", "Concurrent Démo"); click("Confirmer"); await detail(); expect(within(dialog()).getByText("Perdue", { exact: true })).toBeTruthy(); expect(within(dialog()).getByRole("button", { name: "Modifier le devis", exact: true }).disabled).toBe(true);
    await expect(repo.cancel("COT-DEMO-1", {})).rejects.toThrow("ne peut plus");
  });
  it("cancels an open quotation and guards deletion with documents or annex fees", async () => {
    const repo = await ready(); await open(); click("Annuler le devis"); field("Motif d’annulation", "Demande du client"); click("Confirmer"); await detail(); expect(within(dialog()).getByText("Annulée", { exact: true })).toBeTruthy();
    click("Supprimer le devis"); click("Confirmer"); await within(dialog()).findByText("Retirez les documents associés avant de supprimer la cotation.");
    await expect(repo.remove("COT-DEMO-5")).rejects.toThrow("frais annexes");
  });
  it("edits packing through the existing editor", async () => {
    const repo = await ready(); await open(); click("Colisages"); click("Gérer les colisages"); field("Quantité — ligne 1", "4"); click("Ajouter une ligne"); field("Type de colis — ligne 2", "Carton"); click("Enregistrer les lignes"); await detail(); expect((await repo.list()).quotations[0].colisages).toHaveLength(2); expect((await repo.list()).quotations[0].colisages[0].qte).toBe(4);
  });
  it("edits prices with percentage calculations and recomputes totals", async () => {
    await ready(); await open(); click("Prix"); click("Gérer les prix"); field("Quantité — ligne 1", "2"); click("Ajouter une ligne"); field("Libellé — ligne 2", "Surcharge"); field("Calcul — ligne 2", "Pourcentage"); field("Prix unitaire ou taux (%) — ligne 2", "10"); click("Enregistrer les lignes"); await detail(); click("Totaux"); expect(within(dialog()).getByLabelText("Total vente (MAD)").value).toBe("2 200,00"); expect(within(dialog()).getByLabelText("Marge (MAD)").value).toBe("1 600,00");
  });
  it("validates purchase amounts, suppliers and dates", async () => {
    const repo = await ready(); await open(); click("Achats"); click("Gérer les achats"); field("Montant d’achat — ligne 1", "0"); click("Enregistrer les lignes"); expect(within(dialog()).getByText("Le montant d’achat doit être supérieur à zéro.")).toBeTruthy(); field("Montant d’achat — ligne 1", "750"); click("Enregistrer les lignes"); await detail(); expect((await repo.list()).quotations[0].achats[0].mtDevise).toBe(750);
    await expect(repo.setLines("COT-DEMO-1", { kind: "achats", lines: [{ libelle: "Fret", fournisseur: "", mtDevise: 10, devise: "MAD" }] })).rejects.toThrow("fournisseur");
  });
  it("preserves MAD at one and exposes missing foreign exchange instead of false totals", async () => {
    const seed = quotationSeed(); seed.quotations[0].prix[0].devise = "USD"; const repo = createMockQuotationRepository({ seed, delay: 0 }); await ready(repo); await open(); expect(within(dialog()).getByLabelText("Total vente (MAD)").value).toBe("Indisponible");
    click("Taux de change"); click("Gérer les taux"); field("Devise — ligne 1", "USD"); field("Taux vers MAD — ligne 1", "10"); click("Enregistrer les lignes"); await detail(); click("Totaux"); expect(within(dialog()).getByLabelText("Total vente (MAD)").value).toBe("10 000,00"); const row = (await repo.list()).quotations[0]; expect(row.taux.find((line) => line.devise === "MAD").taux).toBe(1);
  });
  it("reuses document upload, stores metadata only, removes attachments then deletes", async () => {
    const repo = await ready(); await open("DEV-DEMO-002"); click("Documents"); click("Ajouter des documents"); const file = new File(["demo"], "devis.txt", { type: "text/plain" }); fireEvent.change(within(dialog()).getByLabelText("Fichiers à ajouter"), { target: { files: [file] } }); click("Ajouter"); await detail(); expect(within(dialog()).getByText("devis.txt")).toBeTruthy(); click("Retirer devis.txt"); click("Confirmer"); await detail(); expect(within(dialog()).getByText("Aucun document associé")).toBeTruthy(); click("Résumé"); click("Supprimer le devis"); click("Confirmer"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect((await repo.list()).quotations).toHaveLength(6);
  });
  it("creates, assigns and removes a regroupement", async () => {
    const repo = await ready(); await open(); click("Regroupements"); field("Nouveau regroupement", "Groupe TEST"); click("Enregistrer le regroupement"); await waitFor(() => expect(within(dialog()).getByLabelText("Nouveau regroupement").value).toBe(""));
    expect((await repo.list()).groups.some((item) => item.label === "Groupe TEST")).toBe(true); field("Regroupement du devis", ""); click("Enregistrer le regroupement"); await within(dialog()).findByText("Aucun regroupement associé"); expect((await repo.list()).quotations[0].groupId).toBe("");
  });
  it("covers loading, errors, retry, initial empty state and failed saves", async () => {
    const seed = quotationSeed(); seed.quotations = []; const repo = createMockQuotationRepository({ seed, delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list);
    render(<QuotationsPage repository={{ ...repo, list, create: vi.fn().mockRejectedValue(new Error("private")) }} />); expect(screen.getByRole("status", { name: "Chargement des cotations…" })).toBeTruthy(); await screen.findByText("Impossible de charger les cotations. Réessayez."); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await screen.findByText("Aucune cotation");
    fireEvent.click(screen.getAllByRole("button", { name: "Créer un devis", exact: true })[0]); field("Numéro de cotation", "TEST"); field("Client du devis", "CLI-DEMO-1"); click("Enregistrer le devis"); await within(dialog()).findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(dialog()).getByLabelText("Numéro de cotation").value).toBe("TEST");
  });
  it("calculates multicurrency totals without mixing percentage bases", () => {
    const row = quotationSeed().quotations[0]; row.prix = [{ quantite: 1, montantValeur: 100, devise: "EUR", typeUtilisation: "Montant" }, { quantite: 1, montantValeur: 10, devise: "EUR", typeUtilisation: "Pourcentage" }, { quantite: 1, montantValeur: 500, devise: "MAD", typeUtilisation: "Montant" }]; const totals = quotationTotals(row); expect(totals.vente).toBe(1688); expect(totals.achat).toBe(600); expect(totals.marge).toBe(1088);
  });
  it("protects the new route and returns to it after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#quotations"); const store = createAuthStore({ tokenSink: vi.fn(), service: { signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte Démo" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Cotations / Devis", exact: true }); expect(window.location.hash).toBe("#quotations"); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
