import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ClientsPage from "../src/Clients/ClientsPage";
import { createMockClientRepository, mockClients } from "../src/Clients/mockClientRepository";

const repository = (options = {}) => createMockClientRepository({ delay: 0, ...options });
const client = (overrides = {}) => ({ ...structuredClone(mockClients[2]), ...overrides });
const ready = () => screen.findByRole("table", { name: "Liste des clients" });
const dialog = (name) => screen.getByRole("dialog", { name });
const fill = (label, value) => fireEvent.change(screen.getByLabelText(label, { exact: true }), { target: { value } });
const openFiche = async (name) => { fireEvent.click(screen.getByRole("button", { name, exact: true })); return dialog("Fiche client"); };

describe("client mock business rules", () => {
  it("enforces unique space-stripped names and ledger accounts on create and update", async () => {
    const source = repository();
    await expect(source.create({ raisonSociale: "AtlasTransitDémo" })).rejects.toMatchObject({ code: "duplicate_name" });
    await expect(source.create({ raisonSociale: "Nouveau client", compteComptable: "411001" })).rejects.toMatchObject({ code: "duplicate_account" });
    await expect(source.create({ raisonSociale: "   " })).rejects.toMatchObject({ code: "required" });
    await expect(source.update(mockClients[2].keyClient, { ...mockClients[2], raisonSociale: mockClients[0].raisonSociale })).rejects.toMatchObject({ code: "duplicate_name" });
    expect(await source.list()).toHaveLength(8);
  });
  it("requires an account, rejects Moroccan identifiers for a foreign client, and does not activate on validation", async () => {
    const source = repository({ clients: [client({ compteComptable: "" })] });
    await expect(source.validate(mockClients[2].keyClient)).rejects.toMatchObject({ code: "account_required" });
    await source.update(mockClients[2].keyClient, client({ compteComptable: "411099", iceClient: "DEMO" }));
    await expect(source.validate(mockClients[2].keyClient)).rejects.toMatchObject({ code: "foreign_identifiers" });
    await source.update(mockClients[2].keyClient, client({ compteComptable: "411099", iceClient: "" }));
    expect(await source.validate(mockClients[2].keyClient)).toMatchObject({ valide: true, actif: false });
    await expect(source.remove(mockClients[2].keyClient)).rejects.toMatchObject({ code: "validated" });
  });
  it("blocks removal while business references or bank accounts exist", async () => {
    const source = repository();
    await expect(source.remove(mockClients[1].keyClient)).rejects.toMatchObject({ code: "referenced" });
    const id = mockClients[2].keyClient;
    const withBank = await source.addBank(id, { banque: "Banque démo", compteRib: "RIB-DEMO-NEW" });
    await expect(source.remove(id)).rejects.toMatchObject({ code: "referenced" });
    await source.removeBank(id, withBank.banques[0].keyBanque); await source.remove(id);
    expect((await source.list()).some((row) => row.keyClient === id)).toBe(false);
  });
  it("protects principal addresses, requires a delivery line/city and enforces RIB uniqueness across clients", async () => {
    const source = repository();
    await expect(source.removeAddress(mockClients[0].keyClient, "ADR-DEMO-1")).rejects.toMatchObject({ code: "principal" });
    await expect(source.addAddress(mockClients[2].keyClient, { ville: "Lyon" })).rejects.toMatchObject({ code: "address_required" });
    await expect(source.addAddress(mockClients[2].keyClient, { adresse1: "Quai démo" })).rejects.toMatchObject({ code: "address_required" });
    await expect(source.addBank(mockClients[2].keyClient, { compteRib: "RIB-DEMO-001" })).rejects.toMatchObject({ code: "duplicate_rib" });
    const updated = await source.addAddress(mockClients[2].keyClient, { adresse2: "Quai démo", ville: "Lyon" });
    expect(updated.adresses[0].type).toBe("Livraison");
    await source.removeAddress(updated.keyClient, updated.adresses[0].keyAdresse);
    expect((await source.list()).find((row) => row.keyClient === updated.keyClient).adresses).toHaveLength(0);
  });
  it("uses private in-memory copies and cancels before a mutation commits", async () => {
    const source = repository(); const rows = await source.list(); rows[0].banques.length = 0;
    expect((await source.list())[0].banques).toHaveLength(1);
    const controller = new AbortController(); controller.abort();
    await expect(source.create({ raisonSociale: "Annulé" }, { signal: controller.signal })).rejects.toMatchObject({ name: "AbortError" });
    expect(await source.list()).toHaveLength(8);
  });
});

describe("Clients page", () => {
  it("shows loading, then paginates and combines accent-insensitive search and filters", async () => {
    let resolve;
    render(<ClientsPage repository={{ list: () => new Promise((done) => { resolve = done; }) }} />);
    expect(screen.getByRole("status", { name: "Chargement des clients…" })).toBeTruthy();
    await act(async () => resolve(structuredClone(mockClients)));
    expect(within(await ready()).getAllByRole("row")).toHaveLength(6);
    fireEvent.click(screen.getByRole("button", { name: "Suivant" }));
    expect(within(await ready()).getAllByRole("row")).toHaveLength(4);
    fill("Rechercher un client", "mediterranee");
    expect(screen.getByRole("button", { name: "Méditerranée Export Démo", exact: true })).toBeTruthy();
    expect(screen.getByRole("button", { name: "1" }).getAttribute("aria-current")).toBe("page");
    fill("Validation du client", "true"); expect(screen.getByText("Aucun résultat")).toBeTruthy();
    fill("Validation du client", "false"); fill("Activité du client", "true");
    expect(screen.getByRole("button", { name: "Méditerranée Export Démo", exact: true })).toBeTruthy();
  });
  it("creates an inactive draft, opens its detail and persists edits through refresh", async () => {
    const source = repository({ clients: [] }); render(<ClientsPage repository={source} />);
    await screen.findByText("Aucun client");
    fireEvent.click(screen.getAllByRole("button", { name: "Créer un client" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    expect(screen.getByText("La raison sociale est obligatoire.")).toBeTruthy();
    fill("Raison sociale", "Client créé Démo"); fill("Pays", "MAROC"); fill("Compte comptable", "411999");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("dialog", { name: "Fiche client" });
    expect(within(dialog("Fiche client")).getByText("Inactif")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Modifier le client", exact: true }));
    fill("Ville", "Casablanca"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("dialog", { name: "Fiche client" });
    expect(within(dialog("Fiche client")).getByText("Casablanca")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Fermer", exact: true }));
    fireEvent.click(screen.getByRole("button", { name: "Actualiser" }));
    await ready(); expect(within(await ready()).getByText("Casablanca")).toBeTruthy();
  });
  it("reports validation errors, lets the user complete the fiche, then validates", async () => {
    render(<ClientsPage repository={repository({ clients: [client({ compteComptable: "" })] })} />); await ready();
    fireEvent.click(screen.getByRole("button", { name: "Valider Azur Distribution Démo" }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await screen.findByText("Un compte comptable est obligatoire pour valider ce client.");
    fireEvent.click(screen.getByRole("button", { name: "Compléter la fiche" })); fill("Compte comptable", "411999");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche client" });
    fireEvent.click(screen.getByRole("button", { name: "Valider le client", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await waitFor(() => expect(within(dialog("Fiche client")).getByText("Validé")).toBeTruthy());
    expect(within(dialog("Fiche client")).getByText("Inactif")).toBeTruthy();
  });
  it("explains a blocked delete, requires confirmation for allowed deletion and clamps pagination", async () => {
    render(<ClientsPage repository={repository()} />); await ready();
    const blocked = screen.getByRole("button", { name: "Supprimer Atlas Transit Démo" });
    expect(blocked.disabled).toBe(true);
    expect(blocked.title).toBe("Un client validé ne peut pas être supprimé.");
    expect(screen.queryByRole("dialog")).toBeNull();
    fill("Rechercher un client", "Azur"); fireEvent.click(screen.getByRole("button", { name: "Supprimer Azur Distribution Démo" }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByText("Aucun résultat")).toBeTruthy();
    fireEvent.click(screen.getAllByRole("button", { name: "Réinitialiser les filtres" })[0]);
    expect(screen.queryByRole("button", { name: "Azur Distribution Démo", exact: true })).toBeNull();
  });
  it("adds and removes delivery addresses and bank accounts from the detail tabs", async () => {
    render(<ClientsPage repository={repository({ clients: [client()] })} />); await ready();
    await openFiche("Azur Distribution Démo"); fireEvent.click(screen.getByRole("button", { name: "Adresses", exact: true }));
    expect(screen.getByText("Aucune adresse complémentaire")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Ajouter une adresse" }));
    fill("Adresse ligne 1", "Quai fictif"); fill("Ville de livraison", "Lyon"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("table", { name: "Adresses du client" });
    expect(screen.getByText("Quai fictif, Lyon")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Retirer l’adresse" })); fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await screen.findByText("Aucune adresse complémentaire");
    fireEvent.click(screen.getByRole("button", { name: "Banques", exact: true }));
    fireEvent.click(screen.getByRole("button", { name: "Ajouter une banque" }));
    fill("Banque", "Banque fictive"); fill("RIB", "RIB-TEST-NEW"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("table", { name: "Banques du client" }); expect(screen.getByText("RIB-TEST-NEW")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Retirer la banque" })); fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await screen.findByText("Aucune coordonnée bancaire");
  });
  it("shows protected principal addresses without offering removal", async () => {
    render(<ClientsPage repository={repository({ clients: [mockClients[0]] })} />); await ready(); await openFiche("Atlas Transit Démo");
    fireEvent.click(screen.getByRole("button", { name: "Adresses", exact: true }));
    expect(screen.getByText("Adresse protégée")).toBeTruthy(); expect(screen.queryByRole("button", { name: "Retirer l’adresse" })).toBeNull();
  });
  it("returns to the remaining page after deleting the only row on the last page", async () => {
    const rows = Array.from({ length: 6 }, (_, index) => client({ keyClient: `ROW-${index}`, raisonSociale: `Client démo ${index}`, compteComptable: `411-${index}` }));
    render(<ClientsPage repository={repository({ clients: rows })} />); await ready();
    fireEvent.click(screen.getByRole("button", { name: "Suivant" }));
    fireEvent.click(screen.getByRole("button", { name: "Supprimer Client démo 5" }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByRole("button", { name: "1" }).getAttribute("aria-current")).toBe("page");
    expect(within(await ready()).getAllByRole("row")).toHaveLength(6);
    expect(screen.getByRole("button", { name: "Suivant" }).disabled).toBe(true);
  });
  it("handles load failures and failed saves without discarding the form or exposing technical details", async () => {
    const source = repository({ failListOnce: true, failActionOnce: true }); render(<ClientsPage repository={source} />);
    await screen.findByText("Impossible de charger les clients. Réessayez."); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await ready();
    fireEvent.click(screen.getByRole("button", { name: "Modifier Azur Distribution Démo" })); fill("Ville", "Nice");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByText("L’opération n’a pas pu aboutir. Réessayez.");
    expect(screen.getByLabelText("Ville", { exact: true }).value).toBe("Nice");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche client" });
    expect(within(dialog("Fiche client")).getByText("Nice")).toBeTruthy();
  });
  it("cancels an outstanding load on unmount and suppresses late results", async () => {
    let signal, resolve;
    const view = render(<ClientsPage repository={{ list: vi.fn((options) => { signal = options.signal; return new Promise((done) => { resolve = done; }); }) }} />);
    view.unmount(); expect(signal.aborted).toBe(true); await act(async () => resolve([]));
  });
});
