import { confirmedBusinessWrite, uncertainBusinessResponse, businessKey, businessPage, createBusinessApi, invalidBusinessResponse } from "./businessApi";
const writable = "raisonSociale refClient typeClient statutClient groupeClient classeClient risqueClient activiteClient detailActivite commercial agenceCommercial agenceExploitation typeAffectCommercial keyChargeClientele adresse1Client adresse2Client cpClient villeClient paysClient departementClient regionClient langueClient telClient faxClient siteWebClient anneeCreation observationClient rcClient centreRcClient patenteClient ifClient cnssClient iceClient deviseFacturation paiementClient echeance nbEcheance finMois plafond compteComptable journalVente journalDebours releveFacture nbFactureReleve ras lieuDedouanement nbJrFranchise immobilisationMead immobilisationPort suiviAt".split(" ");
export function createClientsService({ request }) {
  const call = createBusinessApi(request), cache = new Map(), path = id => `/api/v1/clients/${businessKey(id)}`;
  const mapped = (raw, full = false) => {
    if (!raw || typeof raw.keyClient !== "string" || typeof raw.raisonSociale !== "string" || typeof raw.actif !== "boolean" || typeof raw.valide !== "boolean") throw invalidBusinessResponse();
    if (full && (!Array.isArray(raw.adresses) || !Array.isArray(raw.banques))) throw invalidBusinessResponse();
    return { ...raw, adresses: raw.adresses || [], banques: raw.banques || [] };
  };
  const get = async (id, { signal } = {}) => { const raw = await call(path(id), { signal }); if (raw.keyClient !== id) throw invalidBusinessResponse(); cache.set(id, raw); return mapped(raw, true); };
  const result = row => ({ ...row, row, updatedId: row.keyClient });
  const save = async (id, value, signal) => {
    // PUT replaces the fiche: retain fields not shown in the existing form.
    const original = id ? cache.get(id) || await get(id, { signal }) : {};
    const merged = { ...original, ...value };
    const body = Object.fromEntries(writable.filter(key => merged[key] !== undefined).map(key => [key, merged[key] === "" ? null : merged[key]]));
    const raw = await call(id ? path(id) : "/api/v1/clients", { method: id ? "PUT" : "POST", body, signal, expected: id ? 200 : 201 });
    const row = confirmedBusinessWrite(() => mapped(raw, true)); cache.set(row.keyClient, raw); return result(row);
  };
  const child = async (id, suffix, method, body, signal) => { await call(path(id) + suffix, { method, body, signal, expected: method === "DELETE" ? 204 : 201 }); try { return result(await get(id, { signal })); } catch { throw uncertainBusinessResponse(); } };
  return { remote: true, get,
    async list({ query = "", active = "", validated = "", page = 1, pageSize = 5, signal } = {}) {
      const raw = await businessPage(call, "/api/v1/clients", { search: query.trim(), actif: active, valide: validated, page, pageSize }, signal);
      return { clients: raw.items.map(row => mapped(row)), totalCount: raw.totalCount };
    },
    create: (value, { signal } = {}) => save(null, value, signal),
    update: (id, value, { signal } = {}) => save(id, value, signal),
    async validate(id, { signal } = {}) { const raw = await call(path(id) + "/validate", { method: "POST", signal }); const row = confirmedBusinessWrite(() => mapped(raw, true)); cache.set(id, row); return result(row); },
    async remove(id, { signal } = {}) { await call(path(id), { method: "DELETE", signal, expected: 204 }); cache.delete(id); return { updatedId: null }; },
    addAddress: (id, value, { signal } = {}) => child(id, "/adresses", "POST", value, signal),
    removeAddress: (id, key, { signal } = {}) => child(id, `/adresses/${businessKey(key)}`, "DELETE", undefined, signal),
    addBank: (id, value, { signal } = {}) => child(id, "/banques", "POST", value, signal),
    removeBank: (id, key, { signal } = {}) => child(id, `/banques/${businessKey(key)}`, "DELETE", undefined, signal),
  };
}
