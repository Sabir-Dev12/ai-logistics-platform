import {AuthenticationError} from "./authenticationService";
export class TrackingApiError extends Error{constructor(code,message,{uncertain=false}={}){super(message);this.name="TrackingApiError";this.code=code;this.uncertain=uncertain;}}
const invalid=()=>new TrackingApiError("invalid_response","Le serveur a renvoyé des données de suivi invalides. Actualisez la page.");
const uncertain=()=>new TrackingApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez la chronologie avant une nouvelle tentative.",{uncertain:true});
const rules={"tracking.status":"Ce statut n’est pas autorisé pour cette entité.","tracking.input":"Vérifiez le commentaire et les champs de l’événement.","tracking.entity":"Ce type d’entité n’est pas pris en charge.","tracking.write_failed":"La confirmation n’a pas été reçue. Vérifiez la chronologie avant une nouvelle tentative."};
const entities=["Client","Voyage","Dossier","FactureVente","Reglement","Booking","Cotation"];
export const trackingReferences={Client:{route:"clients",key:"keyClient",label:"raisonSociale",search:"search"},Voyage:{route:"voyages",key:"keyVoyage",label:"numVoyage",search:"search"},Dossier:{route:"dossiers",key:"keyDossier",label:"numDossier",search:"search"},FactureVente:{route:"factures-vente",key:"keyFacture",label:"numFacture"},Reglement:{route:"reglements",key:"keyReglement",label:"referencePaiement"},Booking:{route:"bookings",key:"keyBooking",label:"numBooking",search:"numBooking"},Cotation:{route:"cotations",key:"keyCotation",label:"numCotation",search:"numCotation"}};
const entityName=value=>{const name=Number.isInteger(value)?entities[value]:entities.find(item=>item.toLowerCase()===String(value).toLowerCase());if(!name)throw invalid();return name;};
const dateValue=value=>{if(typeof value!=="string"||!/^\d{4}-\d\d-\d\dT/.test(value))throw invalid();const calendar=new Date(`${value.slice(0,10)}T00:00:00Z`);if(!Number.isFinite(calendar.valueOf())||calendar.toISOString().slice(0,10)!==value.slice(0,10))throw invalid();const text=/(Z|[+-]\d\d:\d\d)$/i.test(value)?value:`${value}Z`;const date=new Date(text);if(!Number.isFinite(date.valueOf()))throw invalid();return date.toISOString();};
const eventRow=(row,index=0)=>{if(!row||typeof row.cleEntite!=="string"||!row.cleEntite||typeof row.isSysteme!=="boolean")throw invalid();const entity=entityName(row.entite),dateSystem=dateValue(row.dateTraceSystem);return {...row,id:`${entity}:${row.cleEntite}:${dateSystem}:${index}`,sequence:-index,entite:entity,dateTraceUser:dateValue(row.dateTraceUser),dateTraceSystem:dateSystem};};
const key=value=>{if(typeof value!=="string"||!value.trim())throw new TrackingApiError("validation","La référence à suivre est obligatoire.");return encodeURIComponent(value.trim());};
export function createTrackingService({request,timeoutMs=15000}){
 const catalogs=new Map();const path=(entity,id)=>`/api/v1/tracking/${entityName(entity)}/${key(id)}`;
  const call = async (url, { signal, method = "GET", body, expected = 200, readOnly = false } = {}) => {
    const controller = new AbortController(), abort = () => controller.abort();
    if (signal?.aborted) controller.abort(); else signal?.addEventListener("abort", abort, { once: true });
    let timedOut = false; const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
    const writing = method !== "GET" && !readOnly;
    try {
      const response = await request(url, { method, signal: controller.signal, headers: { Accept: "application/json", ...(body === undefined ? {} : { "Content-Type": "application/json" }) }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
      if (!response.ok) {
        if (writing && response.status >= 500) throw uncertain();
        let problem; try { problem = await response.json(); } catch { problem = {}; }
        const rule = rules[problem?.rule];
        throw new TrackingApiError(rule || (response.status === 404 ? "missing" : response.status === 409 ? "conflict" : response.status === 400 || response.status === 422 ? "validation" : response.status === 403 ? "forbidden" : "unavailable"),
          rule ? rule : response.status === 404 ? "Cette référence n’est plus disponible. Actualisez la liste." : response.status === 409 ? "Cet événement est en conflit avec les données actuelles." : response.status === 400 || response.status === 422 ? "Les données ont été refusées par le serveur. Vérifiez les champs et les règles métier." : response.status === 403 ? "Vous n’avez pas l’autorisation d’effectuer cette action." : "Le service Suivi est indisponible. Réessayez.");
      }
      if (response.status !== expected) throw invalid();
      if (expected === 204) return null;
      const text = await response.text(); if (!text.trim()) throw invalid();
      try { return JSON.parse(text); } catch { throw invalid(); }
    } catch (error) {
      if (signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (error instanceof AuthenticationError) throw new TrackingApiError(error.code, error.message);
      if (error instanceof TrackingApiError && !(writing && error.code === "invalid_response")) throw error;
      if (writing) throw uncertain();
      if (timedOut) throw new TrackingApiError("timeout", "Le serveur met trop de temps à répondre. Réessayez.");
      throw new TrackingApiError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    } finally { clearTimeout(timer); signal?.removeEventListener("abort", abort); }
  };
 const events=(rows,entity,id)=>{if(!Array.isArray(rows)||rows.length>500)throw invalid();const mapped=rows.map(eventRow);if(mapped.some(row=>row.entite!==entity||row.cleEntite!==id))throw invalid();return mapped;};
 const catalog=async(entity,signal)=>{const rows=await call("/api/v1/tracking/statuts",{signal});if(!Array.isArray(rows)||rows.some(row=>!row||typeof row.keyStatut!=="string"||typeof row.libelleStatut!=="string"||typeof row.isSysteme!=="boolean"))throw invalid();const statuses=rows.filter(row=>!row.isSysteme&&!row.keyStatut.startsWith("$")&&(!row.utilisation?.trim()||row.utilisation.trim().toLowerCase()===entity.toLowerCase())).map(row=>({...row,label:row.libelleStatut,entities:[entity]}));catalogs.set(entity,statuses);return statuses;};
 return {remoteTracking:true,
 async references(entity,{signal,search="",page=1}={}){const config=trackingReferences[entityName(entity)];if(!Number.isInteger(page)||page<1||(!config.search&&search))throw new TrackingApiError("validation","Ces critères de référence ne sont pas disponibles.");const params=new URLSearchParams({page:String(page),pageSize:"20"});if(search.trim())params.set(config.search,search.trim());const result=await call(`/api/v1/${config.route}?${params}`,{signal});if(!result||!Array.isArray(result.items)||result.page!==page||result.pageSize!==20||!Number.isSafeInteger(result.totalCount)||result.totalCount<0||result.items.length>20||result.items.length>result.totalCount||result.items.some(row=>!row||typeof row[config.key]!=="string"))throw invalid();return {...result,items:result.items.map(row=>({key:row[config.key],label:row[config.label]||row[config.key]}))};},
 async list({signal,entity="Client",reference=""}={}){entity=entityName(entity);if(!reference)return {records:[],statuses:await catalog(entity,signal),events:[],latest:null,canRecord:false,limit:500,limited:false};const route=path(entity,reference);const [timeline,status,statuses]=await Promise.all([call(`${route}?take=500`,{signal}),call(`${route}/statut`,{signal}),catalog(entity,signal)]);const mapped=events(timeline,entity,reference);if(!status||entityName(status.entite)!==entity||status.cleEntite!==reference||!Array.isArray(status.derniersEvenements)||!Number.isInteger(status.nbEvenements))throw invalid();const recent=events(status.derniersEvenements,entity,reference);return {records:[],statuses,events:mapped,latest:recent.length?{...recent[0],keyStatut:status.keyStatut,libelleStatut:status.libelleStatut,utilisateur:status.utilisateur,commentaire:status.commentaire}:null,canRecord:statuses.length>0,limit:500,limited:mapped.length===500};},
 async record(entity,id,value,{signal}={}){entity=entityName(entity);let statuses=catalogs.get(entity);if(!statuses)statuses=await catalog(entity,signal);const selected=statuses.find(row=>row.keyStatut===value.keyStatut);if(!selected||value.keyStatut.startsWith("$"))throw new TrackingApiError("validation","Sélectionnez un statut autorisé.");if(!value.commentaire?.trim()||value.commentaire.length>2000||!value.dateEvenement||!Number.isFinite(new Date(value.dateEvenement).valueOf()))throw new TrackingApiError("validation","Vérifiez le commentaire et la date de l’événement.");const body={keyStatut:selected.keyStatut,commentaire:value.commentaire.trim(),dateEvenement:new Date(value.dateEvenement).toISOString()};const row=await call(`${path(entity,id)}/evenements`,{method:"POST",body,signal,expected:201});let created;try{created=eventRow(row);if(created.entite!==entity||created.cleEntite!==id||created.keyStatut!==selected.keyStatut||created.commentaire!==body.commentaire||new Date(created.dateTraceUser).valueOf()!==new Date(body.dateEvenement).valueOf())throw invalid();}catch{throw uncertain();}return {created};},
 };
}
