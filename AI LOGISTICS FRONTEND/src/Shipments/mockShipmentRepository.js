import { billingStatuses, ensure, numberValue, validatePacking, validateShipment } from "./shipmentRules";
export function shipmentSeed() {
  const clients = [{ id: "CLI-DEMO-1", label: "Atlas Transit Démo" }, { id: "CLI-DEMO-2", label: "Azur Distribution Démo" }, { id: "CLI-DEMO-3", label: "Méditerranée Export Démo" }];
  const voyages = [{ id: "VOY-DEMO-1", label: "VOY-DEMO-1 · Casablanca → Tanger" }, { id: "VOY-DEMO-2", label: "VOY-DEMO-2 · Tanger → Marseille" }, { id: "VOY-DEMO-3", label: "VOY-DEMO-3 · Voyage sans dossier" }];
  const shipments = Array.from({ length: 8 }, (_, index) => ({ keyDossier: `DOS-DEMO-${index + 1}`, numDossier: `DOS-DEMO-${String(index + 1).padStart(3, "0")}`, typeDossier: ["RI", "RE", "MI", "ME"][index % 4], keyClient: clients[index % 3].id,
    intituleClient: clients[index % 3].label, keyVoyageInter: index === 6 ? null : voyages[index % 2].id, dateCreation: "2026-10-01", descriptionMarchandise: "Marchandise de démonstration", incoterm: "FCA", nbColis: 10 + index,
    poidsBrut: 100 + index * 50, poidsTaxable: 100 + index * 100, volume: 2 + index, magasinDouane: index % 3 === 1 ? "" : "MEAD Casablanca", statutFacturation: ["NonFacture", "Facture", "ANePasFacturer"][index % 3],
    dateDepotage: index === 2 ? "2026-10-10" : null, lieuDepotage: index === 2 ? "Remorque" : null, colisDepote: index === 2 ? 12 : null, colisSortie: 0,
    colisages: [{ id: `PACK-${index + 1}`, typeColis: "Palette", nbColis: 10 + index, natureMarchandise: "Pièces de démonstration", poidsBrut: 100 + index * 50, volume: 2 + index, marque: "DEMO", codeNgp: "" }] }));
  return { shipments, clients, voyages };
}
function wait(delay, signal) {
  return new Promise((resolve, reject) => {
    const abort = () => { clearTimeout(timer); reject(new DOMException("Chargement annulé", "AbortError")); };
    const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay);
    if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true });
  });
}
export function createMockShipmentRepository({ seed = shipmentSeed(), delay = 250 } = {}) {
  const state = structuredClone(seed); let sequence = 0;
  const snapshot = (updatedId) => ({ ...structuredClone(state), updatedId });
  const find = (id) => { const row = state.shipments.find((item) => item.keyDossier === id); ensure(row, "Le dossier n’est plus disponible."); return row; };
  const save = (payload, id) => {
    validateShipment(payload);
    ensure(!state.shipments.some((row) => row.keyDossier !== id && row.numDossier.toLowerCase() === payload.numDossier.trim().toLowerCase()), "Ce numéro de dossier est déjà utilisé.");
    ensure(!payload.keyClient || state.clients.some((row) => row.id === payload.keyClient), "Le client n’est plus disponible.");
    return { ...payload, numDossier: payload.numDossier.trim(), intituleClient: state.clients.find((row) => row.id === payload.keyClient)?.label || "", ...Object.fromEntries(["nbColis", "poidsBrut", "poidsTaxable", "volume"].map((key) => [key, numberValue(payload[key])])) };
  };
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async create(payload, { signal } = {}) { await wait(delay, signal); const value = save(payload); const id = `DOS-NEW-${++sequence}`; state.shipments.unshift({ ...value, keyDossier: id, dateCreation: new Date().toISOString().slice(0, 10), statutFacturation: "ANePasFacturer", keyVoyageInter: null, colisages: [], dateDepotage: null, colisDepote: null, colisSortie: 0 }); return snapshot(id); },
    async update(id, payload, { signal } = {}) { await wait(delay, signal); const row = find(id); Object.assign(row, save(payload, id)); return snapshot(id); },
    async remove(id, { signal } = {}) { await wait(delay, signal); ensure(find(id).statutFacturation !== "Facture", "Un dossier facturé ne peut pas être supprimé."); state.shipments = state.shipments.filter((row) => row.keyDossier !== id); return snapshot(); },
    async setPacking(id, lines, { signal } = {}) { await wait(delay, signal); const row = find(id); validatePacking(lines); row.colisages = lines.map((line) => ({ ...line, ...Object.fromEntries(["nbColis", "poidsBrut", "volume"].map((key) => [key, numberValue(line[key])])) })); return snapshot(id); },
    async unload(id, payload, { signal } = {}) { await wait(delay, signal); const row = find(id); ensure(Boolean(row.magasinDouane?.trim()), "Le dépotage est réservé aux dossiers MEAD (magasin de douane renseigné)."); ensure(!row.dateDepotage, "Ce dossier est déjà dépoté."); ensure(payload.dateDepotage && !Number.isNaN(Date.parse(payload.dateDepotage)), "Renseignez une date de dépotage valide."); ensure(["Remorque", "Tc"].includes(payload.lieuDepotage), "Sélectionnez le lieu de dépotage."); ensure(payload.colisDepote !== "" && Number.isFinite(Number(payload.colisDepote)) && Number(payload.colisDepote) >= 0, "Le nombre de colis dépotés doit être positif ou nul."); Object.assign(row, payload, { colisDepote: Number(payload.colisDepote) }); return snapshot(id); },
    async cancelUnload(id, { signal } = {}) { await wait(delay, signal); const row = find(id); ensure(Boolean(row.dateDepotage), "Ce dossier n’est pas dépoté."); Object.assign(row, { dateDepotage: null, lieuDepotage: null, constatDepotage: "", refDepotage: "", colisDepote: null, colisSortie: 0 }); return snapshot(id); },
  };
}
export { billingStatuses };
