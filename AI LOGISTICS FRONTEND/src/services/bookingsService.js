import { AuthenticationError } from "./authenticationService";
export class BookingsApiError extends Error {
  constructor(code, message, { uncertain = false } = {}) { super(message); this.name = "BookingsApiError"; this.code = code; this.uncertain = uncertain; }
}
const invalid = () => new BookingsApiError("invalid_response", "Le serveur a renvoyé des données invalides. Actualisez la page.");
const uncertain = () => new BookingsApiError("uncertain", "La confirmation n’a pas été reçue. Vérifiez le résultat avant une nouvelle tentative.", { uncertain: true });
const rules = {
 "booking.validite": "La validité ne peut pas précéder la création.", "booking.port": "Le port de départ est obligatoire.",
 "booking.import.maroc": "Une réservation import ne peut pas partir du Maroc.", "booking.export.maroc": "Une réservation export ne peut pas être destinée au Maroc.",
 "booking.equipement.required": "Ajoutez les équipements avant de sélectionner le chargement Complet.",
 "booking.colis.required": "Ajoutez les colisages avant de sélectionner le chargement Groupage.",
 "booking.capacite": "La capacité restante du voyage est insuffisante.", "booking.depote": "Annulez le dépotage avant de modifier les lignes.",
 "booking.deja_depote": "La réservation est déjà dépotée.", "booking.non_depote": "La réservation n’est pas dépotée.",
 "booking.numero.obligatoire": "Le numéro de réservation est obligatoire."
};
const key = (value) => { if (typeof value !== "string" || !value.trim()) throw new BookingsApiError("validation", "Sélectionnez une référence valide."); return encodeURIComponent(value); };
const pick = (value, fields) => Object.fromEntries(fields.split(" ").filter((field) => value[field] !== undefined).map((field) => [field, typeof value[field] === "string" ? value[field].trim() || null : value[field]]));
const summary = (row) => { if (!row || typeof row.keyBooking !== "string" || !row.keyBooking || typeof row.numBooking !== "string" || typeof row.keyStatutBooking !== "string") throw invalid(); return row; };
const detail = (row) => { summary(row); if (!Array.isArray(row.equipements) || !Array.isArray(row.colisages) || typeof row.depote !== "boolean") throw invalid(); return { ...row, equipements: row.equipements.map((line) => ({...line, id: line.keyLigneEquipement})), colisages: row.colisages.map((line) => ({...line, id: line.keyColisageBooking})) }; };
export function createBookingsService({ request, timeoutMs = 15000 }) {
  const snapshots = new Map();
  const path = (id) => `/api/v1/bookings/${key(id)}`;
  const call = async (url, { signal, method = "GET", body, expected = 200 } = {}) => {
    const controller = new AbortController(), abort = () => controller.abort();
    if (signal?.aborted) controller.abort(); else signal?.addEventListener("abort", abort, { once: true });
    let timedOut = false; const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
    const writing = method !== "GET";
    try {
      const response = await request(url, { method, signal: controller.signal, headers: { Accept: "application/json", ...(body === undefined ? {} : { "Content-Type": "application/json" }) }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
      if (!response.ok) {
        if (writing && response.status >= 500) throw uncertain();
        let problem; try { problem = await response.json(); } catch { problem = {}; }
        const rule = rules[problem?.rule];
        throw new BookingsApiError(rule || (response.status === 404 ? "missing" : response.status === 409 ? "conflict" : response.status === 400 || response.status === 422 ? "validation" : response.status === 403 ? "forbidden" : "unavailable"),
          rule ? rule : response.status === 404 ? "Cette réservation ou cette référence n’est plus disponible. Actualisez la liste." : response.status === 409 ? "Ce numéro de réservation est déjà utilisé." : response.status === 400 || response.status === 422 ? "Les données ont été refusées par le serveur. Vérifiez les champs et les règles métier." : response.status === 403 ? "Vous n’avez pas l’autorisation d’effectuer cette action." : "Le service Réservations est indisponible. Réessayez.");
      }
      if (response.status !== expected) throw invalid();
      if (expected === 204) return null;
      const text = await response.text(); if (!text.trim()) throw invalid();
      try { return JSON.parse(text); } catch { throw invalid(); }
    } catch (error) {
      if (signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (error instanceof AuthenticationError) throw new BookingsApiError(error.code, error.message);
      if (error instanceof BookingsApiError && !(writing && error.code === "invalid_response")) throw error;
      if (writing) throw uncertain();
      if (timedOut) throw new BookingsApiError("timeout", "Le serveur met trop de temps à répondre. Réessayez.");
      throw new BookingsApiError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    } finally { clearTimeout(timer); signal?.removeEventListener("abort", abort); }
  };
  const get = async (id, {signal} = {}) => { const row = detail(await call(path(id), {signal})); if (row.keyBooking !== id) throw invalid(); snapshots.set(id,row); return row; };
  const mutate = async (id, suffix, method, body, signal, expected = 200) => {
    const result = await call(id ? `${path(id)}${suffix}` : "/api/v1/bookings", {method,body,signal,expected});
    if (expected === 204) { snapshots.delete(id); return {updatedId:null}; }
    let row; try { row=detail(result); if (id && !suffix.includes("dupliquer") && row.keyBooking !== id) throw invalid(); } catch { throw uncertain(); }
    snapshots.delete(id); snapshots.set(row.keyBooking,row); return {updatedId:row.keyBooking, booking:row};
  };
  const paged = async (url, signal, page, pageSize) => {
    const result = await call(url,{signal});
    if (!result || !Array.isArray(result.items) || result.page !== page || result.pageSize !== pageSize || !Number.isSafeInteger(result.totalCount) || result.totalCount < 0 || result.items.length > pageSize || result.items.length > result.totalCount) throw invalid();
    return result;
  };
  return {serverPaging:true, get,
    async list({signal,query="",status="",type="",page=1,pageSize=5}={}) {
      if (!Number.isSafeInteger(page) || page<1 || ![5,10,20].includes(pageSize)) throw invalid();
      const params=new URLSearchParams({page:String(page),pageSize:String(pageSize)});
      if(query.trim()) params.set("numBooking",query.trim()); if(status) params.set("keyStatutBooking",status); if(type) params.set("typeBooking",type);
      const result=await paged(`/api/v1/bookings?${params}`,signal,page,pageSize);
      return {...result, bookings:result.items.map(summary)};
    },
    async references(kind,{signal,search="",page=1}={}) {
      if(!["voyages","dossiers"].includes(kind)) throw invalid();
      const params=new URLSearchParams({page:String(page),pageSize:"20"}); if(search.trim()) params.set("search",search.trim());
      const result=await paged(`/api/v1/${kind}?${params}`,signal,page,20);
      const field=kind==="voyages"?"keyVoyage":"keyDossier", number=kind==="voyages"?"numVoyage":"numDossier";
      if(result.items.some((row)=>!row || typeof row[field]!=="string")) throw invalid();
      return {...result,items:result.items.map((row)=>({...row,label:row[number] || row[field]}))};
    },
    async types({signal}={}) { const result=await call("/api/v1/reference/types-dossier",{signal}); if(!Array.isArray(result) || result.some((row)=>typeof row.typeDossierCode!=="string")) throw invalid(); return result.map((row)=>({value:row.typeDossierCode,label:row.intituleType || row.typeDossierCode})); },
    async capacity(id,{signal}={}) { const row=await call(`/api/v1/bookings/capacite/${key(id)}`,{signal}); if(!row || row.keyVoyage!==id || ["capaciteRemorque","volumeReserve","libre","nbBookings","tauxOccupation"].some((field)=>!Number.isFinite(row[field]))) throw invalid(); return row; },
    create:(value,{signal}={})=>mutate(null,"","POST",pick(value,"typeBooking numBooking typeChargement incoterm mode dateValidite keyExpediteur intituleExpediteur paysExpediteur keyDestinataire intituleDestinataire paysDestinataire keyPayeur intitulePayeur descriptionMarchandise commentaire natureOperation"),signal,201),
    async update(id,value,{signal}={}) { const original=snapshots.get(id)||await get(id,{signal}); if(!Object.hasOwn(original,"commentaire") || !Object.hasOwn(original,"natureOperation")) throw new BookingsApiError("contract","Redémarrez le backend mis à jour avant de modifier cette réservation."); return mutate(id,"","PUT",pick({...original,...value},"numBooking typeChargement incoterm mode dateValidite commentaire natureOperation"),signal); },
    validate:(id,{signal}={})=>mutate(id,"/valider","POST",undefined,signal),
    duplicate:(id,value,{signal}={})=>mutate(id,"/dupliquer","POST",pick(value,"numBooking dateValidite"),signal,201),
    cancel:(id,value,{signal}={})=>mutate(id,"/annuler","POST",pick(value,"motif"),signal),
    remove:(id,{signal}={})=>mutate(id,"","DELETE",undefined,signal,204),
    setEquipment:(id,lines,{signal}={})=>mutate(id,"/equipements","PUT",lines.map((line)=>pick(line,"typeEquipement natureEquipement qte")),signal),
    setPacking:(id,lines,{signal}={})=>mutate(id,"/colisages","PUT",lines.map((line)=>pick(line,"typeColis qte poids gerbable longueur largeur hauteur volume typeEquipement natureEquipement")),signal),
    linkVoyage:(id,voyage,{signal}={})=>{key(voyage);return mutate(id,"/voyage","POST",{keyVoyage:voyage},signal);},
    async unlinkVoyage(id,{signal}={}) { const row=await get(id,{signal}),voyage=row.keyVoyagePrevisionnel || row.keyVoyageEnlevement; return mutate(id,`/voyage/${key(voyage)}`,"DELETE",undefined,signal); },
    linkDossier:(id,dossier,{signal}={})=>mutate(id,`/dossier?${new URLSearchParams({keyDossier:decodeURIComponent(key(dossier))})}`,"POST",undefined,signal),
    unlinkDossier:(id,{signal}={})=>mutate(id,"/dossier","DELETE",undefined,signal),
    unload:(id,value,{signal}={})=>mutate(id,"/depotage","POST",pick(value,"dateDepotage constat colisDepote magasin codeBureauDouane"),signal),
    cancelUnload:(id,{signal}={})=>mutate(id,"/depotage","DELETE",undefined,signal),
  };
}
