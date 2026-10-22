import { tokens } from "../design-system/tokens";
import { addressErrors, checkClient, checkValidation, deletionReason, ClientRuleError, requireRule } from "./clientRules";

export const mockClients = [
  ["Atlas Transit Démo", "Casablanca", "MAROC", true, true, ["des dossiers", "des factures"]],
  ["Méditerranée Export Démo", "Tanger", "MAROC", true, false, ["des dossiers"]],
  ["Azur Distribution Démo", "Lyon", "FRANCE", false, false, []],
  ["Sahara Industrie Démo", "Agadir", "MAROC", true, true, ["des règlements"]],
  ["Horizon Commerce Démo", "Rabat", "MAROC", false, false, []],
  ["Nord Import Démo", "Lille", "FRANCE", true, true, []],
  ["Océan Logistique Démo", "Marrakech", "MAROC", false, false, []],
  ["Rivage Services Démo", "Fès", "MAROC", false, false, []],
].map(([raisonSociale, villeClient, paysClient, actif, valide, references], index) => ({
  keyClient: `CLI-DEMO-${index + 1}`, raisonSociale, refClient: `DEMO-${String(index + 1).padStart(3, "0")}`,
  villeClient, paysClient, actif, valide, references, statutClient: "Client", classeClient: index % 2 ? "Standard" : "Grand compte",
  typeClient: "Entreprise", groupeClient: "", risqueClient: "", activiteClient: "Commerce", adresse1Client: "Adresse fictive de démonstration",
  adresse2Client: "", cpClient: "", telClient: "", faxClient: "", siteWebClient: "", observationClient: "Dossier fictif.",
  compteComptable: index === 4 ? "" : `41100${index + 1}`, deviseFacturation: paysClient === "MAROC" ? "MAD" : "EUR",
  paiementClient: "Virement", echeance: "Date de facture", dateCreation: `2026-10-${String(14 - index).padStart(2, "0")}T10:00:00Z`,
  rcClient: "", centreRcClient: "", patenteClient: "", ifClient: "", cnssClient: "", iceClient: "",
  adresses: index === 0 ? [{ keyAdresse: "ADR-DEMO-1", adresse1: "Adresse principale fictive", adresse2: "", cp: "", ville: villeClient, pays: paysClient, type: "Principal", contact: "Contact démo", telAdresse: "" }] : [],
  banques: index === 0 ? [{ keyBanque: "BANK-DEMO-1", banque: "Banque de démonstration", compteRib: "RIB-DEMO-001", codeBanque: "Agence fictive", adresseBanque: villeClient, swift: "", ibanCode: "" }] : [],
}));

const copy = (value) => structuredClone(value);
const trim = (value) => Object.fromEntries(Object.entries(value).map(([key, item]) => [key, typeof item === "string" ? item.trim() : item]));
export function createMockClientRepository({ clients = mockClients, delay = parseInt(tokens.motion.layout, 10), failListOnce = false, failActionOnce = false } = {}) {
  let rows = copy(clients), sequence = 0;
  const wait = (signal) => new Promise((resolve, reject) => {
    const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); };
    const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay);
    if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true });
  });
  const find = (id) => { const row = rows.find((item) => item.keyClient === id); requireRule(Boolean(row), "missing"); return row; };
  const action = async (signal, operation) => {
    await wait(signal);
    if (failActionOnce) { failActionOnce = false; throw new ClientRuleError("unavailable"); }
    return copy(operation());
  };
  return {
    async list({ signal } = {}) { await wait(signal); if (failListOnce) { failListOnce = false; throw new ClientRuleError("unavailable"); } return copy(rows); },
    create: (values, { signal } = {}) => action(signal, () => {
      checkClient(values, rows);
      const client = { ...trim(values), keyClient: `CLI-LOCAL-${++sequence}`, actif: false, valide: false, adresses: [], banques: [], references: [], dateCreation: new Date().toISOString() };
      rows.unshift(client); return client;
    }),
    update: (id, values, { signal } = {}) => action(signal, () => {
      const row = find(id); checkClient(values, rows, id);
      Object.assign(row, trim(values), { compteComptable: values.compteComptable?.trim() || row.compteComptable || "" }); return row;
    }),
    validate: (id, { signal } = {}) => action(signal, () => { const row = find(id); checkValidation(row); row.valide = true; return row; }),
    remove: (id, { signal } = {}) => action(signal, () => {
      const row = find(id), reason = deletionReason(row); if (reason) throw new ClientRuleError(row.valide ? "validated" : "referenced");
      rows = rows.filter((item) => item.keyClient !== id); return id;
    }),
    addAddress: (id, values, { signal } = {}) => action(signal, () => {
      const row = find(id); requireRule(!Object.keys(addressErrors(values)).length, "address_required");
      row.adresses.push({ ...trim(values), keyAdresse: `ADR-LOCAL-${++sequence}`, type: "Livraison" }); return row;
    }),
    removeAddress: (id, addressId, { signal } = {}) => action(signal, () => {
      const row = find(id), address = row.adresses.find((item) => item.keyAdresse === addressId); requireRule(Boolean(address), "missing");
      requireRule(address.type?.toLowerCase() !== "principal", "principal"); row.adresses = row.adresses.filter((item) => item.keyAdresse !== addressId); return row;
    }),
    addBank: (id, values, { signal } = {}) => action(signal, () => {
      const row = find(id), payload = trim(values);
      requireRule(!payload.compteRib || !rows.some((client) => client.banques.some((bank) => bank.compteRib === payload.compteRib)), "duplicate_rib");
      row.banques.push({ ...payload, keyBanque: `BANK-LOCAL-${++sequence}` }); return row;
    }),
    removeBank: (id, bankId, { signal } = {}) => action(signal, () => {
      const row = find(id); requireRule(row.banques.some((bank) => bank.keyBanque === bankId), "missing");
      row.banques = row.banques.filter((bank) => bank.keyBanque !== bankId); return row;
    }),
  };
}
