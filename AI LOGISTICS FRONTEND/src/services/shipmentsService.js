import { AuthenticationError } from "./authenticationService";
export class ShipmentsApiError extends Error {
 constructor(code,message,{uncertain=false}={}){super(message);this.name="ShipmentsApiError";this.code=code;this.uncertain=uncertain;}
}
const invalid=()=>new ShipmentsApiError("invalid_response","Le serveur a renvoyé des données invalides. Actualisez la page.");
const uncertain=()=>new ShipmentsApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez le résultat avant une nouvelle tentative.",{uncertain:true});
const rules={"dossier.delete.invoiced":"Un dossier facturé ne peut pas être supprimé.","dossier.dangerous_goods.class":"Une classe est obligatoire pour les marchandises dangereuses.","dossier.poids_taxable":"Le poids taxable doit être positif ou nul.","dossier.temperature":"La température minimale ne peut pas dépasser la température maximale.","dossier.depotage.not_mead":"Le dépotage est réservé aux dossiers MEAD."};
const key=value=>{if(typeof value!=="string"||!value.trim())throw new ShipmentsApiError("validation","Sélectionnez une référence valide.");return encodeURIComponent(value);};
const pick=(value,fields)=>Object.fromEntries(fields.filter(field=>value[field]!==undefined).map(field=>[field,typeof value[field]==="string"?value[field].trim()||null:value[field]]));
const enumValue=(value,names)=>{if(Number.isInteger(value)&&names[value])return names[value];if(names.includes(value))return value;throw invalid();};
const summary=row=>{if(!row||typeof row.keyDossier!=="string"||!row.keyDossier||typeof row.numDossier!=="string")throw invalid();return {...row,statutFacturation:enumValue(row.statutFacturation,["ANePasFacturer","NonFacture","Facture"])};};
const detail=row=>{const value=summary(row);if(!Array.isArray(row.colisages)||row.colisages.some(line=>!Number.isSafeInteger(line.keyLigneColisage)||line.keyLigneColisage<1))throw invalid();return {...value,lieuDepotage:row.lieuDepotage==null?null:enumValue(row.lieuDepotage,["Remorque","Tc"]),colisages:row.colisages.map(line=>({...line,id:line.keyLigneColisage}))};};
const saveFields=["numDossier", "typeDossier", "keyClient", "intituleClient", "natureOperation", "serviceDossier", "position", "commentaireDossier", "keyAgenceExploitation", "intituleAgenceExploitation", "keyExpediteur", "intituleExpediteur", "keyDestinataire", "intituleDestinataire", "keyPayeur", "intitulePayeur", "keyAgent", "intituleAgent", "incoterm", "descriptionMarchandise", "marchandiseGerbable", "marchandiseDangereuse", "classeMarchandiseDangereuse", "marchandiseFragile", "assurance", "nbColis", "typeColis", "marque", "poidsBrut", "volume", "poidsTaxable", "volumeTaxable", "magasinDouane", "hsCode", "codeRegime", "codeBureau", "montantFret", "deviseFret", "temperatureDirigee", "temperatureMin", "temperatureMax"];
const numeric=["position","nbColis","poidsBrut","volume","poidsTaxable","volumeTaxable","montantFret","temperatureMin","temperatureMax"];
const savePayload=value=>{const result=pick(value,saveFields);for(const field of numeric){if(result[field]!=null){if(!Number.isFinite(Number(result[field]))||(field==="position"&&!Number.isInteger(Number(result[field]))))throw new ShipmentsApiError("validation","Renseignez des nombres valides.");result[field]=Number(result[field]);}}if(!result.numDossier||!result.typeDossier)throw new ShipmentsApiError("validation","Le numéro et le type du dossier sont obligatoires.");return result;};
export function createShipmentsService({request,timeoutMs=15000}){
 const snapshots=new Map();const path=id=>`/api/v1/dossiers/${key(id)}`;
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
        throw new ShipmentsApiError(rule || (response.status === 404 ? "missing" : response.status === 409 ? "conflict" : response.status === 400 || response.status === 422 ? "validation" : response.status === 403 ? "forbidden" : "unavailable"),
          rule ? rule : response.status === 404 ? "Ce dossier ou cette référence n’est plus disponible. Actualisez la liste." : response.status === 409 ? "Ce numéro de dossier est déjà utilisé." : response.status === 400 || response.status === 422 ? "Les données ont été refusées par le serveur. Vérifiez les champs et les règles métier." : response.status === 403 ? "Vous n’avez pas l’autorisation d’effectuer cette action." : "Le service Dossiers est indisponible. Réessayez.");
      }
      if (response.status !== expected) throw invalid();
      if (expected === 204) return null;
      const text = await response.text(); if (!text.trim()) throw invalid();
      try { return JSON.parse(text); } catch { throw invalid(); }
    } catch (error) {
      if (signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (error instanceof AuthenticationError) throw new ShipmentsApiError(error.code, error.message);
      if (error instanceof ShipmentsApiError && !(writing && error.code === "invalid_response")) throw error;
      if (writing) throw uncertain();
      if (timedOut) throw new ShipmentsApiError("timeout", "Le serveur met trop de temps à répondre. Réessayez.");
      throw new ShipmentsApiError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    } finally { clearTimeout(timer); signal?.removeEventListener("abort", abort); }
  };
 const get=async(id,{signal}={})=>{const row=detail(await call(path(id),{signal}));if(row.keyDossier!==id)throw invalid();snapshots.set(id,row);return row;};
 const paged=async(url,signal,page,pageSize)=>{const result=await call(url,{signal});if(!result||!Array.isArray(result.items)||result.page!==page||result.pageSize!==pageSize||!Number.isSafeInteger(result.totalCount)||result.totalCount<0||result.items.length>pageSize||result.items.length>result.totalCount)throw invalid();return result;};
 const mutate=async(id,suffix,method,body,signal,expected=200)=>{const result=await call(id?`${path(id)}${suffix}`:"/api/v1/dossiers",{method,body,signal,expected});if(expected===204){snapshots.delete(id);return {updatedId:method==="DELETE"&&suffix===""?null:id};}let row;try{row=detail(result);if(id&&row.keyDossier!==id)throw invalid();}catch{throw uncertain();}snapshots.set(row.keyDossier,row);return {updatedId:row.keyDossier};};
 const numericPacking=value=>{const result=pick(value,["typeColis","nbColis","natureMarchandise","marque","codeNgp","poidsBrut","volume"]);if(!result.typeColis||result.nbColis==null)throw new ShipmentsApiError("validation","Le type et le nombre de colis sont obligatoires.");for(const field of ["nbColis","poidsBrut","volume"]){if(result[field]!=null){result[field]=Number(result[field]);if(!Number.isFinite(result[field])||result[field]<0)throw new ShipmentsApiError("validation","Les mesures doivent être positives ou nulles.");}}return result;};
 return {serverPaging:true,get,
 async list({signal,query="",client="",voyage="",type="",billing="",page=1,pageSize=5}={}){if(!Number.isSafeInteger(page)||page<1||![5,10,20].includes(pageSize)||!["","ANePasFacturer","NonFacture","Facture"].includes(billing))throw invalid();const params=new URLSearchParams({page:String(page),pageSize:String(pageSize)});for(const [name,value] of Object.entries({search:query.trim(),keyClient:client,keyVoyage:voyage,typeDossier:type,statutFacturation:billing}))if(value)params.set(name,value);const result=await paged(`/api/v1/dossiers?${params}`,signal,page,pageSize);return {...result,shipments:result.items.map(summary)};},
 async references(kind,{signal,search="",page=1}={}){if(!["clients","voyages"].includes(kind)||!Number.isInteger(page)||page<1)throw invalid();const params=new URLSearchParams({page:String(page),pageSize:"20"});if(search.trim())params.set("search",search.trim());const result=await paged(`/api/v1/${kind}?${params}`,signal,page,20);const field=kind==="clients"?"keyClient":"keyVoyage";if(result.items.some(row=>!row||typeof row[field]!=="string"))throw invalid();return {...result,items:result.items.map(row=>({...row,label:kind==="clients"?row.raisonSociale||row.keyClient:row.numVoyage||row.keyVoyage}))};},
 async types({signal}={}){const result=await call("/api/v1/reference/types-dossier",{signal});if(!Array.isArray(result)||result.some(row=>typeof row.typeDossierCode!=="string"))throw invalid();return result.map(row=>({value:row.typeDossierCode,label:row.intituleType||row.typeDossierCode}));},
 create:(value,{signal}={})=>mutate(null,"","POST",savePayload(value),signal,201),
 async update(id,value,{signal}={}){const row=snapshots.get(id)||await get(id,{signal});if(!row.editableFields||saveFields.some(field=>!Object.hasOwn(row.editableFields,field)))throw new ShipmentsApiError("contract","Redémarrez le backend mis à jour avant de modifier ce dossier.");return mutate(id,"","PUT",savePayload({...row.editableFields,...value}),signal);},
 remove:(id,{signal}={})=>mutate(id,"","DELETE",undefined,signal,204),
 unload:(id,value,{signal}={})=>{const body=pick(value,["dateDepotage","lieuDepotage","constatDepotage","colisDepote","refDepotage"]);if(!body.dateDepotage||!Number.isFinite(Date.parse(body.dateDepotage))||!["Remorque","Tc"].includes(body.lieuDepotage))throw new ShipmentsApiError("validation","Vérifiez la date et le lieu de dépotage.");body.lieuDepotage=body.lieuDepotage==="Tc"?1:0;if(body.colisDepote!=null){body.colisDepote=Number(body.colisDepote);if(!Number.isFinite(body.colisDepote)||body.colisDepote<0)throw new ShipmentsApiError("validation","Les colis dépotés doivent être positifs ou nuls.");}return mutate(id,"/depotage","POST",body,signal);},
 cancelUnload:(id,{signal}={})=>mutate(id,"/depotage/annulation","POST",undefined,signal),
 async addPacking(id,value,{signal}={}){const result=await call(`${path(id)}/colisages`,{method:"POST",body:numericPacking(value),signal,expected:201});if(!result||!Number.isSafeInteger(result.keyLigneColisage)||result.keyLigneColisage<1)throw uncertain();snapshots.delete(id);return {updatedId:id};},
 removePacking:(id,line,{signal}={})=>{if(!Number.isSafeInteger(line)||line<1)throw new ShipmentsApiError("validation","Sélectionnez un colisage valide.");return mutate(id,`/colisages/${line}`,"DELETE",undefined,signal,204);},
 async byVoyage(id,{signal}={}){const rows=await call(`/api/v1/dossiers/by-voyage/${key(id)}`,{signal});if(!Array.isArray(rows))throw invalid();return rows.map(summary);},
 async allocation(id,mad,currency,{signal}={}){if([mad,currency].some(value=>value===""||!Number.isFinite(Number(value))||Number(value)<0))throw new ShipmentsApiError("validation","Les montants doivent être positifs ou nuls.");const params=new URLSearchParams({montantMad:String(mad),montantDevise:String(currency)});const rows=await call(`/api/v1/dossiers/by-voyage/${key(id)}/repartition?${params}`,{signal});if(!Array.isArray(rows)||rows.some(row=>typeof row.keyDossier!=="string"||!Number.isFinite(row.montantMad)||!Number.isFinite(row.montantDevise)))throw invalid();return rows;},
 };
}
