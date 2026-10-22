import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import BookingsPage from "../src/Bookings/BookingsPage";
import { createMockBookingRepository, createMockBookings, mockVoyages } from "../src/Bookings/mockBookingRepository";
import { validateBooking, voyageCapacity } from "../src/Bookings/bookingRules";

const source = (options = {}) => createMockBookingRepository({ delay: 0, ...options });
const ready = () => screen.findByRole("table", { name: "Liste des réservations" });
const popup = (name) => screen.getByRole("dialog", { name });
const fill = (label, value) => fireEvent.change(within(screen.getByRole("dialog")).getByLabelText(label, { exact: true }), { target: { value } });
const openBooking = async (number = "RES-DEMO-003") => { fireEvent.click(screen.getByRole("button", { name: number, exact: true })); return popup("Fiche réservation"); };

describe("booking mock rules", () => {
  it("keeps unique numbers even after soft deletion, and keeps number/type immutable during modification", async () => {
    const repo = source();
    await expect(repo.create({ numBooking: "RES-DEMO- 001", typeBooking: "RI" })).rejects.toMatchObject({ code: "duplicate" });
    await expect(repo.create({ numBooking: " ", typeBooking: "RI" })).rejects.toMatchObject({ code: "required" });
    await repo.remove("BKG-DEMO-3");
    expect((await repo.list()).bookings).toHaveLength(7);
    await expect(repo.create({ numBooking: "RES-DEMO-003", typeBooking: "RI" })).rejects.toMatchObject({ code: "duplicate" });
    const result = await repo.update("BKG-DEMO-5", { numBooking: "AUTRE", typeBooking: "RE", commentaire: "Mis à jour" });
    expect(result.bookings.find((row) => row.keyBooking === "BKG-DEMO-5")).toMatchObject({ numBooking: "RES-DEMO-005", typeBooking: "RI", commentaire: "Mis à jour" });
  });
  it("duplicates lines with new identifiers and clears voyage, dossier and unloading", async () => {
    const repo = source(); const before = (await repo.list()).bookings[0];
    const result = await repo.duplicate(before.keyBooking, { numBooking: "COPIE-DEMO" });
    const copy = result.bookings.find((row) => row.keyBooking === result.updatedId);
    expect(copy).toMatchObject({ keyStatutBooking: "$BOKC", keyDossier: null, keyVoyagePrevisionnel: null, keyVoyageEnlevement: null, depote: false, volumePrevision: 0 });
    expect(copy.equipements[0].id).not.toBe(before.equipements[0].id); expect(copy.colisages[0].id).not.toBe(before.colisages[0].id);
    copy.equipements[0].qte = 99;
    expect((await repo.list()).bookings.find((row) => row.keyBooking === before.keyBooking).equipements[0].qte).toBe(1);
  });
  it.each([
    [{ dateValidite: "2000-01-01" }, "validity"], [{ dateDepartPrevue: "2027-01-01", portDepart: "" }, "port"],
    [{ typeBooking: "RI", paysExpediteur: "MAROC" }, "origin"], [{ typeBooking: "RE", paysDestinataire: "MAROC" }, "destination"],
    [{ dateDepartPrevue: "2027-01-01", portDepart: "Port démo", dateDisponibiliteMarchandise: "2027-01-02" }, "availability"],
    [{ typeChargement: "Complet", equipements: [] }, "equipment"], [{ typeChargement: "Groupage", nbTotalColis: 0 }, "packages"],
  ])("validates booking consistency without inventing a validated state (%s)", (changes, code) => {
    expect(() => validateBooking({ ...createMockBookings()[0], ...changes })).toThrow(expect.objectContaining({ code }));
  });
  it("validates successfully without altering the business status", async () => {
    const repo = source(); const result = await repo.validate("BKG-DEMO-3");
    expect(result.bookings.find((row) => row.keyBooking === "BKG-DEMO-3").keyStatutBooking).toBe("$ATRM");
  });
  it("checks forecast capacity, prevents two voyage assignments and blocks cancellation/deletion until detached", async () => {
    const repo = source();
    await expect(repo.linkVoyage("BKG-DEMO-3", "VOY-DEMO-3")).rejects.toMatchObject({ code: "capacity" });
    const result = await repo.linkVoyage("BKG-DEMO-3", "VOY-DEMO-1");
    expect(voyageCapacity(mockVoyages[0], result.bookings)).toMatchObject({ volumeReserve: 50, libre: 30, nbBookings: 2, tauxOccupation: 62.5 });
    await expect(repo.linkVoyage("BKG-DEMO-3", "VOY-DEMO-2")).rejects.toMatchObject({ code: "voyage" });
    await expect(repo.cancel("BKG-DEMO-3", {})).rejects.toMatchObject({ code: "linked" });
    await expect(repo.remove("BKG-DEMO-3")).rejects.toMatchObject({ code: "linked" });
    await repo.unlinkVoyage("BKG-DEMO-3");
    const pickup = await repo.linkVoyage("BKG-DEMO-3", "VOY-DEMO-2");
    expect(pickup.bookings.find((row) => row.keyBooking === "BKG-DEMO-3")).toMatchObject({ finEnlevement: true, keyVoyageEnlevement: "VOY-DEMO-2" });
    await repo.unlinkVoyage("BKG-DEMO-3"); const cancelled = await repo.cancel("BKG-DEMO-3", { motif: "Démonstration" });
    expect(cancelled.bookings.find((row) => row.keyBooking === "BKG-DEMO-3")).toMatchObject({ keyStatutBooking: "$BOKAN", commentaireStatutBooking: "Démonstration" });
    await expect(repo.linkVoyage("BKG-DEMO-3", "VOY-DEMO-1")).rejects.toMatchObject({ code: "cancelled" });
  });
  it("maintains reciprocal dossier links and refuses another booking’s dossier", async () => {
    const repo = source();
    await expect(repo.linkDossier("BKG-DEMO-3", "DOS-DEMO-1")).rejects.toMatchObject({ code: "dossier" });
    let result = await repo.linkDossier("BKG-DEMO-3", "DOS-DEMO-2");
    expect(result.dossiers[1].keyBooking).toBe("BKG-DEMO-3");
    expect(result.bookings.find((row) => row.keyBooking === "BKG-DEMO-3")).toMatchObject({ keyDossier: "DOS-DEMO-2", keyStatutBooking: "$BOCV" });
    result = await repo.unlinkDossier("BKG-DEMO-3"); expect(result.dossiers[1].keyBooking).toBeNull();
    expect(result.bookings.find((row) => row.keyBooking === "BKG-DEMO-3")).toMatchObject({ keyDossier: null, keyStatutBooking: "$ATRM" });
  });
  it("supports equipment/packing replacement, locks lines during unloading, and clears the unloading counters on reversal", async () => {
    const repo = source(); await expect(repo.setEquipment("BKG-DEMO-3", [{ qte: -1 }])).rejects.toMatchObject({ code: "number" });
    await expect(repo.setEquipment("BKG-DEMO-3", [{ qte: null }])).rejects.toMatchObject({ code: "number" });
    let result = await repo.setPacking("BKG-DEMO-3", [{ typeColis: "Palette", qte: 2, gerbable: false, hauteur: 3 }]);
    let booking = result.bookings.find((row) => row.keyBooking === "BKG-DEMO-3");
    expect(booking.nbTotalColis).toBe(2); expect(booking.colisages[0].hauteurNgerbable).toBe(3); expect(booking.volumePrevision).toBe(30);
    result = await repo.unload("BKG-DEMO-3", { dateDepotage: "2026-10-15", colisDepote: 2, magasin: "Démo" });
    expect(result.bookings.find((row) => row.keyBooking === "BKG-DEMO-3")).toMatchObject({ depote: true, libelleStatutMead: "En Stock" });
    await expect(repo.unload("BKG-DEMO-3", { dateDepotage: "2026-10-15" })).rejects.toMatchObject({ code: "already_unloaded" });
    await expect(repo.setPacking("BKG-DEMO-3", [])).rejects.toMatchObject({ code: "unloaded" });
    await expect(repo.setEquipment("BKG-DEMO-3", [])).rejects.toMatchObject({ code: "unloaded" });
    result = await repo.cancelUnload("BKG-DEMO-3"); booking = result.bookings.find((row) => row.keyBooking === "BKG-DEMO-3");
    expect(booking).toMatchObject({ depote: false, dateDebutDepotage: null, colisDepote: null, colisSortie: 0, libelleStatutMead: "Attente Dépotage" });
    await repo.setPacking("BKG-DEMO-3", []);
  });
  it("aborts before committing a mutation and returns isolated copies", async () => {
    const repo = source(), controller = new AbortController(); controller.abort();
    await expect(repo.remove("BKG-DEMO-3", { signal: controller.signal })).rejects.toMatchObject({ name: "AbortError" });
    const snapshot = await repo.list(); snapshot.bookings.length = 0; expect((await repo.list()).bookings).toHaveLength(8);
  });
});

describe("Bookings page", () => {
  it("loads, paginates, searches and combines status/type filters", async () => {
    render(<BookingsPage repository={source()} />); expect(screen.getByRole("status", { name: "Chargement des réservations…" })).toBeTruthy();
    expect(within(await ready()).getAllByRole("row")).toHaveLength(6);
    fireEvent.click(screen.getByRole("button", { name: "Suivant" })); expect(within(await ready()).getAllByRole("row")).toHaveLength(4);
    fireEvent.change(screen.getByLabelText("Rechercher une réservation"), { target: { value: "mediterranee" } });
    expect(screen.getByRole("button", { name: "1" }).getAttribute("aria-current")).toBe("page");
    fireEvent.change(screen.getByLabelText("Statut de réservation"), { target: { value: "$BOKAN" } });
    fireEvent.change(screen.getByLabelText("Type de réservation"), { target: { value: "RI" } }); expect(screen.getByText("Aucun résultat")).toBeTruthy();
  });
  it("creates a draft, modifies its header and duplicates it", async () => {
    render(<BookingsPage repository={source({ bookings: [] })} />); await screen.findByText("Aucune réservation");
    fireEvent.click(screen.getAllByRole("button", { name: "Créer une réservation" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); expect(screen.getByText("Le numéro est obligatoire.")).toBeTruthy();
    fill("Numéro de réservation", "NOUVELLE-DEMO"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    fireEvent.click(screen.getByRole("button", { name: "Modifier la réservation", exact: true })); fill("Commentaire", "Commentaire modifié");
    expect(within(popup("Modifier la réservation")).getByLabelText("Numéro de réservation").readOnly).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    expect(within(popup("Fiche réservation")).getByText("Commentaire modifié")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Dupliquer la réservation", exact: true })); fill("Nouveau numéro (facultatif)", "COPIE-DEMO");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    expect(within(popup("Fiche réservation")).getByText("Dupliquée")).toBeTruthy();
  });
  it("edits equipment and packing, then validates without a fake validated status", async () => {
    render(<BookingsPage repository={source()} />); await ready(); await openBooking();
    fireEvent.click(screen.getByRole("button", { name: "Équipements et colisages", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Gérer les équipements", exact: true }));
    fill("Quantité — ligne 1", "3"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer les lignes" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    expect(within(screen.getByRole("table", { name: "Équipements réservés" })).getByText("3")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Gérer les colisages", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Ajouter une ligne" })); fill("Quantité — ligne 2", "2");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer les lignes" })); await screen.findByRole("table", { name: "Colisages réservés" });
    expect(within(screen.getByRole("table", { name: "Colisages réservés" })).getAllByRole("row")).toHaveLength(3);
    fireEvent.click(screen.getByRole("button", { name: "Résumé", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Valider la réservation", exact: true }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmer" })); await screen.findByText("Contrôles de validation réussis. Le statut de la réservation est inchangé.");
    expect(within(popup("Fiche réservation")).getByText("En attente de rattachement")).toBeTruthy();
  });
  it("shows a voyage’s capacity and links a voyage and dossier from the detail", async () => {
    render(<BookingsPage repository={source()} />); await ready();
    fireEvent.click(screen.getByRole("button", { name: "Capacité d’un voyage" })); fill("Voyage", "VOY-DEMO-1");
    expect(within(popup("Capacité d’un voyage")).getByText("Volume libre (m³)").parentElement.textContent).toContain("60");
    fireEvent.click(screen.getByRole("button", { name: "Fermer", exact: true })); await openBooking();
    fireEvent.click(screen.getByRole("button", { name: "Voyages et dossiers", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Associer un voyage", exact: true }));
    fill("Voyage", "VOY-DEMO-3"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByText("La capacité restante du voyage est insuffisante.");
    fill("Voyage", "VOY-DEMO-1"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    expect(screen.getByRole("button", { name: "Désaffecter le voyage" }).disabled).toBe(false);
    fireEvent.click(screen.getByRole("button", { name: "Associer un dossier", exact: true })); fill("Dossier", "DOS-DEMO-2"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("dialog", { name: "Fiche réservation" }); expect(within(popup("Fiche réservation")).getByText("Dossier de démonstration 2")).toBeTruthy();
  });
  it("records and reverses unloading, with equipment locked in between", async () => {
    render(<BookingsPage repository={source()} />); await ready(); await openBooking();
    fireEvent.click(screen.getByRole("button", { name: "Dépotage", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Enregistrer le dépotage", exact: true }));
    fill("Colis dépotés", "2"); fill("Magasin", "Magasin fictif"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("dialog", { name: "Fiche réservation" }); expect(within(popup("Fiche réservation")).getByText("Dépoté")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Équipements et colisages", exact: true })); expect(screen.getByRole("button", { name: "Gérer les équipements", exact: true }).disabled).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Dépotage", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Annuler le dépotage", exact: true }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    expect(within(popup("Fiche réservation")).getByText("Non dépoté")).toBeTruthy();
  });
  it("cancels, then confirms deletion of an unassigned reservation", async () => {
    render(<BookingsPage repository={source()} />); await ready(); await openBooking();
    fireEvent.click(screen.getByRole("button", { name: "Annuler la réservation", exact: true })); fill("Motif d’annulation (facultatif)", "Test d’annulation");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
    expect(within(popup("Fiche réservation")).getByText("Annulée")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Supprimer la réservation", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Confirmer" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect(screen.queryByRole("button", { name: "RES-DEMO-003", exact: true })).toBeNull();
  });
  it("handles load errors and mutation errors, retains the form, and retries", async () => {
    render(<BookingsPage repository={source({ failListOnce: true, failActionOnce: true })} />);
    await screen.findByText("Impossible de charger les réservations. Réessayez."); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await ready();
    fireEvent.click(screen.getByRole("button", { name: "Modifier RES-DEMO-003" })); fill("Commentaire", "Saisie conservée"); fireEvent.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(popup("Modifier la réservation")).getByLabelText("Commentaire").value).toBe("Saisie conservée");
    fireEvent.click(screen.getByRole("button", { name: "Enregistrer" })); await screen.findByRole("dialog", { name: "Fiche réservation" });
  });
  it("aborts an outstanding load on unmount", async () => {
    let signal, resolve;
    const view = render(<BookingsPage repository={{ list: (options) => { signal = options.signal; return new Promise((done) => { resolve = done; }); } }} />);
    view.unmount(); expect(signal.aborted).toBe(true); await act(async () => resolve({ bookings: [], voyages: [], dossiers: [] }));
  });
});
