import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SalesInvoicesPage from "../src/SalesInvoices/SalesInvoicesPage";
import { createMockInvoiceRepository, invoiceSeed } from "../src/SalesInvoices/mockInvoiceRepository";
import { calculateInvoice, newLine, round, validateInvoice } from "../src/SalesInvoices/invoiceRules";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
const dialog = () => screen.getByRole("dialog");
const click = (name) => fireEvent.click(within(dialog()).getByRole("button", { name, exact: true }));
const field = (label, value) => fireEvent.change(within(dialog()).getByLabelText(label, { exact: true }), { target: { value } });
async function ready(repo = createMockInvoiceRepository({ delay: 0 })) { render(<SalesInvoicesPage repository={repo} />); await screen.findByRole("table", { name: "Liste des factures de vente" }); return repo; }
async function detail() { await screen.findByRole("dialog", { name: "Fiche facture de vente" }); }
async function open(number = "FV-2026-001") { fireEvent.click(screen.getByRole("button", { name: number, exact: true })); await detail(); }
describe("Factures de vente", () => {
  it("filters client, type, status, settlement, dates and searches without accents", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Suivant", exact: true })); expect(screen.getByText("6–7 sur 7 documents")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Client facturé"), { target: { value: "CLI-DEMO-1" } }); expect(screen.getByText("1–4 sur 4 documents")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Rechercher une facture"), { target: { value: "atlas transit demo" } }); expect(screen.getByText("1–4 sur 4 documents")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Factures du"), { target: { value: "2026-10-04" } }); expect(screen.getByText("1–2 sur 2 documents")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Factures au"), { target: { value: "2026-10-01" } }); expect(screen.getByText("La date de fin doit être postérieure ou égale à la date de début.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Réinitialiser les filtres", exact: true })); fireEvent.change(screen.getByLabelText("État de règlement"), { target: { value: "true" } }); expect(screen.getByText("1–1 sur 1 documents")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Statut de facture"), { target: { value: "Contestée" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy();
  });
  it("shows detail lines, VAT recap and invoice totals with clear currencies", async () => {
    await ready(); await open(); expect(within(dialog()).getByRole("table", { name: "Lignes de facture" })).toBeTruthy(); expect(within(dialog()).getByRole("table", { name: "Récapitulatif TVA des lignes" })).toBeTruthy(); expect(within(dialog()).getByLabelText("Total HT (MAD)").value).toBe("1 100,00"); expect(within(dialog()).getByLabelText("Net à payer (MAD)").value).toBe("1 300,00");
  });
  it("creates a discounted foreign currency invoice with generated number and line operations", async () => {
    const repo = await ready(); fireEvent.click(screen.getByRole("button", { name: "Créer une facture", exact: true })); click("Créer la facture"); expect(within(dialog()).getByText("Sélectionnez un client.")).toBeTruthy();
    field("Client de la facture", "CLI-DEMO-1"); field("Devise de facture", "EUR"); field("Taux facture vers MAD", "10"); field("Libellé — ligne 1", "Fret export"); field("Montant brut — ligne 1", "100"); field("Remise (%) — ligne 1", "10"); field("Taux vers MAD — ligne 1", "10"); click("Ajouter une ligne"); click("Retirer la ligne 2"); expect(within(dialog()).getByLabelText("Net à payer (EUR)").value).toBe("108,00"); click("Créer la facture"); await detail(); const row = (await repo.list()).invoices[0]; expect(row.number).toMatch(/^FV-DEMO-N/); expect(row.totals.ht).toBe(900); expect(row.totals.ttcMad).toBe(1080); expect(row.totals.net).toBe(108);
    click("Fermer"); fireEvent.click(screen.getByRole("button", { name: "Actualiser", exact: true })); await screen.findByRole("button", { name: row.number, exact: true });
  });
  it("rejects missing lines, reversed due dates and duplicate invoice numbers", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Créer une facture", exact: true })); field("Client de la facture", "CLI-DEMO-1"); field("Date de facture", "2026-10-16"); field("Date d’échéance", "2026-01-01"); click("Créer la facture"); expect(within(dialog()).getByText("Vérifiez les dates de facture et d’échéance.")).toBeTruthy(); field("Date d’échéance", "2026-12-31"); click("Retirer la ligne 1"); click("Créer la facture"); expect(within(dialog()).getByText("Une facture doit comporter au moins une ligne.")).toBeTruthy(); click("Ajouter une ligne"); field("Libellé — ligne 1", "Fret"); field("Montant brut — ligne 1", "0"); field("Numéro de facture", "FV-2026-001"); click("Créer la facture"); await within(dialog()).findByText("Ce numéro de facture est déjà utilisé."); expect(within(dialog()).getByLabelText("Numéro de facture").value).toBe("FV-2026-001");
  });
  it("applies exemption then recalculates without silently presenting stale totals", async () => {
    const repo = await ready(); await open(); click("Appliquer une exonération"); field("Commentaire d’exonération", "Attestation Démo"); click("Confirmer"); await detail(); expect(within(dialog()).getByText("Recalcul nécessaire")).toBeTruthy(); expect(within(dialog()).getByRole("button", { name: "Créer un avoir total", exact: true }).disabled).toBe(true); const before = (await repo.list()).invoices[0]; expect(before.totals.vat).toBe(200); expect(before.lines[0].vatRate).toBe(0);
    click("Recalculer les montants"); click("Confirmer"); await detail(); expect(within(dialog()).getByLabelText("TVA (MAD)").value).toBe("0,00"); expect(within(dialog()).getByLabelText("Net à payer (MAD)").value).toBe("1 100,00"); expect(within(dialog()).getByText("Attestation Démo")).toBeTruthy(); expect((await repo.list()).invoices[0].needsRecalculation).toBe(false);
  });
  it("creates a negative total credit without changing source amounts or settlements", async () => {
    const repo = await ready(); await open(); click("Créer un avoir total"); click("Annuler"); expect((await repo.list()).invoices).toHaveLength(7); click("Créer un avoir total"); click("Confirmer"); await detail(); const rows = (await repo.list()).invoices, credit = rows[0], source = rows.find((row) => row.id === "FV-DEMO-1"); expect(credit.type).toBe("Avoir"); expect(credit.totals.net).toBe(-1300); expect(credit.lines[0].gross).toBe(-1000); expect(credit.lines[0].id).not.toBe(source.lines[0].id); expect(source.totals.net).toBe(1300); expect(source.paid).toBe(0); expect(credit.settled).toBe(false); expect(within(dialog()).getByRole("button", { name: "Créer un avoir total", exact: true }).disabled).toBe(true);
    click("Recalculer les montants"); click("Confirmer"); await detail(); expect((await repo.list()).invoices[0].totals.net).toBe(-1300);
  });
  it("supports the simulated settled credit option", async () => {
    const repo = await ready(); await open("FV-2026-002"); click("Créer un avoir total"); field("Règlement de l’avoir", "true"); click("Confirmer"); await detail(); const credit = (await repo.list()).invoices[0]; expect(credit.settled).toBe(true); expect(credit.paid).toBe(credit.totals.net); expect(within(dialog()).getByLabelText("Solde (MAD)").value).toBe("0,00");
  });
  it("guards ineffective exemptions and crediting a credit document", async () => {
    const seed = invoiceSeed(); seed.invoices[0].lines.forEach((line) => { line.vatRate = 0; line.originalVatRate = 0; }); const repo = createMockInvoiceRepository({ seed, delay: 0 }); await expect(repo.exempt("FV-DEMO-1", { comment: "Démo" })).rejects.toThrow("Aucune ligne"); const result = await repo.credit("FV-DEMO-1", { settled: false }); await expect(repo.credit(result.updatedId, { settled: false })).rejects.toThrow("depuis une facture");
  });
  it("calculates four HT buckets and line-rounded VAT, independently of charges", () => {
    const row = invoiceSeed().invoices[0]; row.lines = ["Produit", "Debours", "Assigne", "TaxeRegionale", "Charge"].map((nature, index) => ({ ...newLine(`L${index}`), label: nature, nature, gross: 100, discount: 10, rate: 2, vatRate: 20 })); row.rate = 2; const totals = calculateInvoice(row); expect(totals.ht).toBe(720); expect(totals.vat).toBe(180); expect(totals.ttcMad).toBe(900); expect(totals.net).toBe(450); expect(round(-1.005)).toBe(-1.01); expect(round(1.005)).toBe(1.01);
  });
  it("validates nonfinite amounts, bounded percentages and exchange rates", () => {
    const seed = invoiceSeed(), row = seed.invoices[0]; row.lines[0].discount = 101; expect(() => validateInvoice(row, seed.clients)).toThrow("remise"); row.lines[0].discount = 0; row.lines[0].rate = 0; expect(() => validateInvoice(row, seed.clients)).toThrow("strictement positif"); row.lines[0].rate = 1; row.lines[0].gross = Infinity; expect(() => validateInvoice(row, seed.clients)).toThrow("montant brut"); row.lines[0].gross = 1e15; expect(() => calculateInvoice(row)).toThrow("plage de calcul");
  });
  it("renders loading, load failure, retry, empty state and preserves failed forms", async () => {
    const seed = invoiceSeed(); seed.invoices = []; const repo = createMockInvoiceRepository({ seed, delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list); render(<SalesInvoicesPage repository={{ ...repo, list, create: vi.fn().mockRejectedValue(new Error("private")) }} />); expect(screen.getByRole("status", { name: "Chargement des factures…" })).toBeTruthy(); await screen.findByText("Impossible de charger les factures. Réessayez."); fireEvent.click(screen.getByRole("button", { name: "Réessayer", exact: true })); await screen.findByText("Aucune facture"); fireEvent.click(screen.getAllByRole("button", { name: "Créer une facture", exact: true })[0]); field("Client de la facture", "CLI-DEMO-1"); field("Libellé — ligne 1", "Fret"); field("Montant brut — ligne 1", "100"); click("Créer la facture"); await within(dialog()).findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(dialog()).getByLabelText("Libellé — ligne 1").value).toBe("Fret");
  });
  it("aborts a pending credit creation without side effects", async () => {
    const repo = createMockInvoiceRepository({ delay: 10 }), controller = new AbortController(); const pending = repo.credit("FV-DEMO-1", { settled: false }, { signal: controller.signal }); controller.abort(); await expect(pending).rejects.toMatchObject({ name: "AbortError" }); expect((await repo.list()).invoices).toHaveLength(7);
  });
  it("protects the route and returns to sales invoices after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#sales-invoices"); const store = createAuthStore({ tokenSink: vi.fn(), service: { authorizedRequest: async (url) => {const params=new URL(url,"http://localhost").searchParams;return new Response(JSON.stringify({items:[],page:Number(params.get("page")||1),pageSize:Number(params.get("pageSize")||20),totalCount:0}),{status:200});}, signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte Démo" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Factures de vente", exact: true }); expect(window.location.hash).toBe("#sales-invoices"); await waitFor(() => expect(screen.getAllByRole("button", { name: "Créer une facture", exact: true })[0].disabled).toBe(false)); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
