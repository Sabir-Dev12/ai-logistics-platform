import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ReferencesPage from "../src/ReferenceData/ReferencesPage";
import { createMockReferenceRepository, referenceSeed } from "../src/ReferenceData/mockReferenceRepository";
import { catalog } from "../src/ReferenceData/referenceCatalog";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
const click = (name) => fireEvent.click(screen.getByRole("button", { name, exact: true }));
const category = (value) => fireEvent.change(screen.getByLabelText("Référentiel à consulter"), { target: { value } });
const field = (label, value) => fireEvent.change(within(screen.getByRole("dialog")).getByLabelText(label, { exact: true }), { target: { value } });
const sample = { code: "GBP", label: "Livre sterling", decimals: "2", invoiceLabel: "Livres", minorUnit: "Penny" };
async function ready(repo = createMockReferenceRepository({ delay: 0 })) { render(<ReferencesPage repository={repo} />); await screen.findByRole("table", { name: "Pays", exact: true }); return repo; }
describe("Référentiels / Paramètres", () => {
  it("consults every catalog, searches without accents and paginates", async () => {
    await ready(); expect(screen.getByText("1–5 sur 6 références")).toBeTruthy(); click("Suivant"); expect(screen.getByText("6–6 sur 6 références")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Rechercher une référence"), { target: { value: "ESPAGNE" } }); expect(screen.getByText("1–1 sur 1 références")).toBeTruthy(); fireEvent.change(screen.getByLabelText("Rechercher une référence"), { target: { value: "absent" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy();
    for (const [key, item] of Object.entries(catalog).filter(([key]) => key !== "company")) { category(key); expect(screen.getByRole("table", { name: item.label, exact: true })).toBeTruthy(); }
    category("company"); expect(screen.getByLabelText("Raison sociale").value).toContain("Démonstration"); expect(screen.getByLabelText("Raison sociale").readOnly).toBe(true); expect(screen.queryByText("Référence société")).toBeNull(); expect(screen.queryByRole("button", { name: "Enregistrer" })).toBeNull();
  });
  it("creates a country and validates required fields and alpha-2", async () => {
    const repo = await ready(); click("Créer un pays"); click("Enregistrer"); expect(screen.getByText("Utilisez un code de 1 à 20 caractères.")).toBeTruthy(); field("Code", "belgique"); field("Libellé", "Belgique"); field("Code alpha-2", "1!"); click("Enregistrer"); expect(screen.getByText("Le code alpha-2 contient deux lettres.")).toBeTruthy(); field("Code alpha-2", "BE"); field("Accord", "Démo"); click("Enregistrer"); await screen.findByRole("heading", { name: "Détail — Pays" }); expect(within(screen.getByRole("dialog")).getByLabelText("Code").value).toBe("BELGIQUE"); expect((await repo.list()).entries.countries[0].alpha2).toBe("BE");
  });
  it("creates currencies with explicit rounding precision and invoice labels", async () => {
    const repo = await ready(); category("currencies"); click("Créer une devise"); field("Code", sample.code); field("Libellé", sample.label); field("Nombre de décimales", "3"); field("Libellé facture", sample.invoiceLabel); field("Unité divisionnaire", sample.minorUnit); click("Enregistrer"); await screen.findByRole("heading", { name: "Détail — Devises" }); expect((await repo.list()).entries.currencies[0]).toMatchObject({ code: "GBP", decimals: 3, invoiceLabel: "Livres", minorUnit: "Penny" });
  });
  it("rejects duplicate codes and alpha-2, invalid precision and forbidden creation atomically", async () => {
    const repo = createMockReferenceRepository({ delay: 0 }); await expect(repo.create("countries", { code: "maroc", label: "Test" })).rejects.toThrow("code existe"); await expect(repo.create("countries", { code: "TEST", label: "Test", alpha2: "MA" })).rejects.toThrow("alpha-2 existe"); for (const decimals of ["-1", "2.5", "", "7"]) await expect(repo.create("currencies", { ...sample, decimals })).rejects.toThrow("entier"); await expect(repo.create("agencies", sample)).rejects.toThrow("non autorisée"); expect((await repo.list()).entries.countries).toHaveLength(6);
  });
  it("blocks deletion of referenced records and reports usages", async () => {
    const repo = await ready(); click("MAROC"); expect(screen.getByRole("button", { name: "Supprimer", exact: true }).disabled).toBe(true); click("Vérifier les références"); await screen.findByText("Suppression bloquée"); expect(screen.getByRole("table", { name: "Utilisations de la référence" }).textContent).toContain("Clients"); expect(screen.getByRole("button", { name: "Supprimer", exact: true }).disabled).toBe(true); const check = await repo.check("countries", "MAROC"); await expect(repo.remove("countries", "MAROC", check.checkId)).rejects.toThrow("refusée"); expect((await repo.list()).entries.countries).toHaveLength(6);
  });
  it("requires a matching check and explicit confirmation, supports cancellation then deletion", async () => {
    const repo = await ready(); await expect(repo.remove("countries", "ESPAGNE", "missing")).rejects.toThrow("Vérifiez"); click("ESPAGNE"); click("Vérifier les références"); await screen.findByText("Vérification favorable"); click("Supprimer"); click("Annuler"); expect((await repo.list()).entries.countries).toHaveLength(6); click("Supprimer"); click("Confirmer la suppression"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect((await repo.list()).entries.countries.some((row) => row.id === "ESPAGNE")).toBe(false);
  });
  it("enforces mock permissions and read-only catalogs even when methods are called directly", async () => {
    const seed = referenceSeed(); seed.canDelete = false; seed.canCreate = false; const repo = await ready(createMockReferenceRepository({ seed, delay: 0 })); expect(screen.getByRole("button", { name: "Créer un pays" }).disabled).toBe(true); click("ESPAGNE"); click("Vérifier les références"); await screen.findByText("Votre profil ne permet pas la suppression."); expect(screen.getByRole("button", { name: "Supprimer", exact: true }).disabled).toBe(true); await expect(repo.create("currencies", sample)).rejects.toThrow("non autorisée"); const check = await repo.check("countries", "ESPAGNE"); await expect(repo.remove("countries", "ESPAGNE", check.checkId)).rejects.toThrow("refusée");
    const unrestricted = createMockReferenceRepository({ delay: 0 }); const readOnly = await unrestricted.check("dossierTypes", "IMPORT"); expect(readOnly.canDelete).toBe(false); await expect(unrestricted.remove("dossierTypes", "IMPORT", readOnly.checkId)).rejects.toThrow("refusée");
  });
  it("soft deletes agencies and incoterms but physically removes other unused records", async () => {
    const repo = createMockReferenceRepository({ delay: 0 }); for (const [kind, id] of [["agencies", "TNG"], ["incoterms", "EXW"]]) { const check = await repo.check(kind, id); expect(check.softDelete).toBe(true); await repo.remove(kind, id, check.checkId); expect((await repo.list()).entries[kind].find((row) => row.id === id).deleted).toBe(true); await expect(repo.check(kind, id)).rejects.toThrow("n’existe plus"); }
  });
  it("does not reuse a check for another record and rechecks references on deletion", async () => {
    const repo = createMockReferenceRepository({ delay: 0 }); const check = await repo.check("countries", "ESPAGNE"); await expect(repo.remove("countries", "PORTUGAL", check.checkId)).rejects.toThrow("Vérifiez"); const state = await repo.list(); state.entries.countries.find((row) => row.id === "ESPAGNE").references.push({ label: "Nouveau client", count: 1 }); expect((await repo.list()).entries.countries.find((row) => row.id === "ESPAGNE").references).toEqual([]); await repo.remove("countries", "ESPAGNE", check.checkId); await expect(repo.remove("countries", "ESPAGNE", check.checkId)).rejects.toThrow("Vérifiez");
  });
  it("shows read-only details for parameters and dossier types", async () => {
    await ready(); for (const [kind, code] of [["settings", "param_oblig_port"], ["dossierTypes", "IMPORT"]]) { category(kind); click(code); expect(screen.queryByRole("button", { name: "Supprimer", exact: true })).toBeNull(); expect(screen.getByText("Ce référentiel est disponible uniquement en consultation.")).toBeTruthy(); click("Fermer"); }
  });
  it("covers loading, retry, empty company/catalog and retained failed form values", async () => {
    const seed = referenceSeed(); seed.entries.countries = []; seed.company = null; const repo = createMockReferenceRepository({ seed, delay: 0 }); render(<ReferencesPage repository={{ ...repo, list: vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list), create: vi.fn().mockRejectedValue(new Error("private")) }} />); expect(screen.getByRole("status", { name: "Chargement des référentiels…" })).toBeTruthy(); await screen.findByText("Impossible de charger les référentiels. Réessayez."); click("Réessayer"); await screen.findByText("Aucune référence"); category("company"); expect(screen.getByText("Aucune société disponible")).toBeTruthy(); category("countries"); click("Créer un pays"); field("Code", "TEST"); field("Libellé", "Test"); click("Enregistrer"); await screen.findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(screen.getByRole("dialog")).getByLabelText("Code").value).toBe("TEST"); expect(screen.queryByText("private")).toBeNull();
  });
  it("keeps failed checks from authorizing deletion and displays their error", async () => {
    const repo = createMockReferenceRepository({ delay: 0 }); await ready({ ...repo, check: vi.fn().mockRejectedValue(new Error("private")) }); click("ESPAGNE"); click("Vérifier les références"); await screen.findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(screen.getByRole("button", { name: "Supprimer", exact: true }).disabled).toBe(true);
  });
  it("aborts a pending creation without mutating data", async () => {
    const repo = createMockReferenceRepository({ delay: 10 }), controller = new AbortController(); const pending = repo.create("currencies", sample, { signal: controller.signal }); controller.abort(); await expect(pending).rejects.toMatchObject({ name: "AbortError" }); expect((await repo.list()).entries.currencies).toHaveLength(3);
  });
  it("protects the route and returns to references after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#references"); const store = createAuthStore({ tokenSink: vi.fn(), service: { authorizedRequest: async (url) => new Response(JSON.stringify(url.endsWith("/moi") ? {estAdmin:false} : []),{status:200}), signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte Démo" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Référentiels / Paramètres", exact: true }); expect(window.location.hash).toBe("#references"); await screen.findByRole("table", { name: "Pays", exact: true }); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
