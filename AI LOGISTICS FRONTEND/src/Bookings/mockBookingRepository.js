import { tokens } from "../design-system/tokens";
import { bookingGuard, BookingRuleError, checkNumbers, linkedVoyage, validateBooking, voyageCapacity } from "./bookingRules";

export const mockVoyages = [
  { keyVoyage: "VOY-DEMO-1", label: "Tanger → Lyon · Prévisionnel", categorie: "Previsionnel", capaciteRemorque: 80 },
  { keyVoyage: "VOY-DEMO-2", label: "Casablanca · Enlèvement", categorie: "Enlevement", capaciteRemorque: 40 },
  { keyVoyage: "VOY-DEMO-3", label: "Agadir → Madrid · Prévisionnel", categorie: "Previsionnel", capaciteRemorque: 10 },
  { keyVoyage: "VOY-DEMO-4", label: "Voyage sans capacité renseignée", categorie: "Previsionnel", capaciteRemorque: 0 },
];
export const mockDossiers = Array.from({ length: 5 }, (_, index) => ({ keyDossier: `DOS-DEMO-${index + 1}`, label: `Dossier de démonstration ${index + 1}`, keyBooking: index === 0 ? "BKG-DEMO-2" : null }));
export function createMockBookings(now = new Date()) {
  return Array.from({ length: 8 }, (_, index) => ({
    keyBooking: `BKG-DEMO-${index + 1}`, numBooking: `RES-DEMO-${String(index + 1).padStart(3, "0")}`,
    typeBooking: index % 2 ? "RE" : "RI", typeChargement: index % 2 ? "Groupage" : "Complet",
    dateCreation: new Date(now.getTime() - (index + 1) * 86400000).toISOString(), dateValidite: now.toISOString().slice(0, 10),
    keyStatutBooking: index === 1 ? "$BOCV" : index === 5 ? "$BOKAN" : "$ATRM", commentaireStatutBooking: index === 5 ? "Annulation fictive" : "",
    intituleClient: ["Atlas Transit Démo", "Azur Distribution Démo", "Méditerranée Export Démo"][index % 3],
    intituleExpediteur: "Expéditeur de démonstration", paysExpediteur: index % 2 ? "MAROC" : "FRANCE",
    intituleDestinataire: "Destinataire de démonstration", paysDestinataire: index % 2 ? "FRANCE" : "MAROC",
    incoterm: "FCA", mode: "Route", commentaire: "Données fictives.", natureOperation: "Transport", descriptionMarchandise: "Marchandise de démonstration",
    portDepart: "", dateDepartPrevue: "", dateDisponibiliteMarchandise: "", marchandiseGerbable: true,
    volumePrevision: [20, 10, 30, 5, 12, 0, 8, 3][index], poidsBrutPrevision: 300, nbTotalColis: 4,
    keyVoyagePrevisionnel: index === 0 ? "VOY-DEMO-1" : null, keyVoyageEnlevement: null, finEnlevement: false,
    keyDossier: index === 1 ? "DOS-DEMO-1" : null, supprime: false,
    depote: index === 3, dateDebutDepotage: index === 3 ? now.toISOString().slice(0, 10) : null,
    colisDepote: index === 3 ? 4 : null, colisSortie: 0, magasin: index === 3 ? "Magasin démo" : "", codeBureauDouane: "", constatDepotageBooking: "",
    keyStatutBookingMead: index === 3 ? "$STOCKB" : "$ATDEPOTB", libelleStatutMead: index === 3 ? "En Stock" : "Attente Dépotage",
    equipements: [{ id: `EQ-DEMO-${index}`, typeEquipement: "Remorque", natureEquipement: "Standard", qte: 1 }],
    colisages: [{ id: `COL-DEMO-${index}`, typeColis: "Palette", qte: 4, poids: 300, gerbable: true, longueur: null, largeur: null, hauteur: null, volume: null, typeEquipement: "", natureEquipement: "" }],
  }));
}
const copy = (value) => structuredClone(value);
export function createMockBookingRepository({ bookings = createMockBookings(), voyages = mockVoyages, dossiers = mockDossiers, delay = parseInt(tokens.motion.layout, 10), failListOnce = false, failActionOnce = false } = {}) {
  let rows = copy(bookings), trips = copy(voyages), files = copy(dossiers), sequence = 0;
  const snapshot = (updatedId = null) => ({ bookings: copy(rows.filter((row) => !row.supprime)), voyages: copy(trips), dossiers: copy(files), updatedId });
  const wait = (signal) => new Promise((resolve, reject) => {
    const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); };
    const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay);
    if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true });
  });
  const requireBooking = (id) => { const booking = rows.find((row) => row.keyBooking === id && !row.supprime); bookingGuard(Boolean(booking), "missing"); return booking; };
  const unique = (num, id) => bookingGuard(!rows.some((row) => row.keyBooking !== id && row.numBooking.replace(/ /g, "").toLowerCase() === num.replace(/ /g, "").toLowerCase()), "duplicate");
  const operation = async (signal, action) => { await wait(signal); if (failActionOnce) { failActionOnce = false; throw new BookingRuleError("unavailable"); } const id = action(); return snapshot(id); };
  return {
    async list({ signal } = {}) { await wait(signal); if (failListOnce) { failListOnce = false; throw new BookingRuleError("unavailable"); } return snapshot(); },
    create: (values, { signal } = {}) => operation(signal, () => {
      bookingGuard(Boolean(values.numBooking?.trim() && values.typeBooking), "required"); unique(values.numBooking);
      const row = { ...createMockBookings()[0], ...copy(values), keyBooking: `BKG-LOCAL-${++sequence}`, numBooking: values.numBooking.trim(), dateCreation: new Date().toISOString(),
        keyStatutBooking: "$ATRM", keyVoyagePrevisionnel: null, keyVoyageEnlevement: null, finEnlevement: false, keyDossier: null,
        equipements: [], colisages: [], volumePrevision: 0, poidsBrutPrevision: null, nbTotalColis: 0 };
      rows.unshift(row); return row.keyBooking;
    }),
    update: (id, values, { signal } = {}) => operation(signal, () => { const row = requireBooking(id);
      Object.assign(row, copy(values), { numBooking: row.numBooking, typeBooking: row.typeBooking }); return id; }),
    validate: (id, { signal } = {}) => operation(signal, () => { validateBooking(requireBooking(id)); return id; }),
    duplicate: (id, values, { signal } = {}) => operation(signal, () => {
      const source = requireBooking(id), numBooking = values.numBooking?.trim() || `RES-LOCAL-${Date.now()}-${++sequence}`; unique(numBooking);
      const row = { ...copy(source), keyBooking: `BKG-LOCAL-${++sequence}`, numBooking, dateCreation: new Date().toISOString(), dateValidite: values.dateValidite || new Date().toISOString().slice(0, 10),
        keyStatutBooking: "$BOKC", keyDossier: null, keyVoyagePrevisionnel: null, keyVoyageEnlevement: null, finEnlevement: false,
        depote: false, dateDebutDepotage: null, colisDepote: null, colisSortie: 0, magasin: "", codeBureauDouane: "", constatDepotageBooking: "", keyStatutBookingMead: null, libelleStatutMead: "",
        volumePrevision: 0, poidsBrutPrevision: null, nbTotalColis: null, commentaireStatutBooking: "" };
      row.equipements.forEach((line) => { line.id = `EQ-LOCAL-${++sequence}`; }); row.colisages.forEach((line) => { line.id = `COL-LOCAL-${++sequence}`; });
      rows.unshift(row); return row.keyBooking;
    }),
    cancel: (id, values, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(!linkedVoyage(row), "linked"); bookingGuard(row.keyStatutBooking !== "$BOKAN", "cancelled");
      row.keyStatutBooking = "$BOKAN"; row.commentaireStatutBooking = values.motif || ""; return id; }),
    remove: (id, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(!linkedVoyage(row), "linked"); row.supprime = true; row.keyStatutBooking = "$SBOK"; return null; }),
    setEquipment: (id, lines, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(!row.depote, "unloaded"); lines.forEach((line) => { bookingGuard(line.qte != null && line.qte !== "", "number"); checkNumbers([line.qte]); });
      row.equipements = copy(lines).map((line) => ({ ...line, id: `EQ-LOCAL-${++sequence}` })); return id; }),
    setPacking: (id, lines, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(!row.depote, "unloaded");
      lines.forEach((line) => checkNumbers([line.qte, line.poids, line.longueur, line.largeur, line.hauteur, line.volume]));
      row.colisages = copy(lines).map((line) => ({ ...line, id: `COL-LOCAL-${++sequence}`, hauteurNgerbable: line.gerbable ? null : line.hauteur }));
      row.nbTotalColis = row.colisages.reduce((sum, line) => sum + Number(line.qte || 0), 0); return id; }),
    linkVoyage: (id, voyageId, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(row.keyStatutBooking !== "$BOKAN", "cancelled"); bookingGuard(!linkedVoyage(row), "voyage");
      const trip = trips.find((item) => item.keyVoyage === voyageId); bookingGuard(Boolean(trip), "missing");
      bookingGuard(trip.capaciteRemorque <= 0 || voyageCapacity(trip, rows).libre >= Number(row.volumePrevision || 0), "capacity");
      row[trip.categorie === "Previsionnel" ? "keyVoyagePrevisionnel" : "keyVoyageEnlevement"] = voyageId; row.finEnlevement = trip.categorie !== "Previsionnel"; return id; }),
    unlinkVoyage: (id, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); row.keyVoyagePrevisionnel = null; row.keyVoyageEnlevement = null; row.finEnlevement = false; return id; }),
    linkDossier: (id, dossierId, { signal } = {}) => operation(signal, () => { const row = requireBooking(id), file = files.find((item) => item.keyDossier === dossierId); bookingGuard(Boolean(file), "missing");
      bookingGuard(row.keyStatutBooking !== "$BOKAN", "cancelled"); bookingGuard((!row.keyDossier || row.keyDossier === dossierId) && (!file.keyBooking || file.keyBooking === id), "dossier");
      row.keyDossier = dossierId; file.keyBooking = id; row.keyStatutBooking = "$BOCV"; return id; }),
    unlinkDossier: (id, { signal } = {}) => operation(signal, () => { const row = requireBooking(id), file = files.find((item) => item.keyDossier === row.keyDossier);
      if (file?.keyBooking === id) file.keyBooking = null; row.keyDossier = null; row.keyStatutBooking = "$ATRM"; return id; }),
    unload: (id, values, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(!row.depote, "already_unloaded"); checkNumbers([values.colisDepote]); bookingGuard(Boolean(values.dateDepotage) && Number.isFinite(Date.parse(values.dateDepotage)), "date");
      Object.assign(row, { depote: true, dateDebutDepotage: values.dateDepotage, colisDepote: values.colisDepote, magasin: values.magasin, codeBureauDouane: values.codeBureauDouane, constatDepotageBooking: values.constat, keyStatutBookingMead: "$STOCKB", libelleStatutMead: "En Stock" }); return id; }),
    cancelUnload: (id, { signal } = {}) => operation(signal, () => { const row = requireBooking(id); bookingGuard(row.depote, "not_unloaded");
      Object.assign(row, { depote: false, dateDebutDepotage: null, colisDepote: null, colisSortie: 0, constatDepotageBooking: "", keyStatutBookingMead: "$ATDEPOTB", libelleStatutMead: "Attente Dépotage" }); return id; }),
  };
}
