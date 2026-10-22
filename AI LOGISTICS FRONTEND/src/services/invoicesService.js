import {AuthenticationError} from "./authenticationService";
import {InvoiceError,natures,validateInvoice} from "../SalesInvoices/invoiceRules";
export class InvoicesApiError extends Error{constructor(code,message,{uncertain=false}={}){super(message);this.name="InvoicesApiError";this.code=code;this.uncertain=uncertain;}}
const invalid=()=>new InvoicesApiError("invalid_response","Le serveur a renvoyé des données invalides. Actualisez la page.");
const uncertain=()=>new InvoicesApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez la facture avant de recommencer.",{uncertain:true});
const rules={"facture.lignes":"Une facture doit comporter au moins une ligne.","avoir.source":"Un avoir total ne peut être créé que depuis une facture.","exoneration.ineffective":"Aucune ligne n’est concernée par cette exonération."};
export const invoiceTypes={Facture:70,Avoir:65,DebitNote:68,CreditNote:67,Od:79,Impaye:73};
const kind=value=>{if(Object.hasOwn(invoiceTypes,value))return value;const found=Object.keys(invoiceTypes).find(name=>invoiceTypes[name]===value);if(!found)throw invalid();return found;};
const key=value=>{if(typeof value!=="string"||!value.trim())throw new InvoiceError("Sélectionnez une référence valide.");return encodeURIComponent(value);};
const date=value=>{if(!value)return null;if(!/^\d{4}-\d{2}-\d{2}$/.test(value)||!Number.isFinite(Date.parse(value))||new Date(value+"T00:00:00Z").toISOString().slice(0,10)!==value)throw new InvoiceError("Renseignez une date valide.");return value+"T00:00:00";};
const summary=row=>{if(!row||typeof row.keyFacture!=="string"||!row.keyFacture||typeof row.deviseFacture!=="string"||!["ttcDevise","netAPayer","mtRegle"].every(field=>Number.isFinite(row[field]))||typeof row.factRegle!=="boolean")throw invalid();return {...row,id:row.keyFacture,number:row.numFacture||row.keyFacture,clientId:row.keyClient||"",clientName:row.intituleClient||row.keyClient||"—",type:kind(row.typeFacture),date:row.dateFacture?.slice(0,10)||"",dueDate:row.dateEcheance?.slice(0,10)||"",currency:row.deviseFacture,status:row.statutFacture||"",settled:row.factRegle,paid:row.mtRegle,totals:{ttc:row.ttcDevise,net:row.netAPayer}};};
const detail=row=>{const value=summary(row);if(!Array.isArray(row.lignes)||!Array.isArray(row.tva)||!["htMad","deboursMad","assigneMad","taxeRegionale","totalHtMad","tvaMad","ttcMad","rasIs","rasTva","tauxDeviseFacture","encoursMad"].every(field=>Number.isFinite(row[field])))throw invalid();return {...value,remote:true,rate:row.tauxDeviseFacture,dossier:row.keyDossier||"",voyage:row.keyVoyageInt||"",service:row.natureService||"",comment:row.commentaireFacture||"",outstanding:row.encoursMad,
 needsRecalculation:row.tvaMad!==0&&row.lignes.length>0&&row.lignes.every(line=>line.tauxTva===0),
 totals:{...value.totals,produit:row.htMad,debours:row.deboursMad,assigne:row.assigneMad,regional:row.taxeRegionale,ht:row.totalHtMad,vat:row.tvaMad,ttcMad:row.ttcMad,rasIs:row.rasIs,rasTva:row.rasTva},
 lines:row.lignes.map(line=>{const nature=typeof line.natureLigne==="number"?natures[line.natureLigne]:line.natureLigne;if(typeof line.keyLigne!=="string"||!natures.includes(nature)||!["mtTotalDevise","tauxRemise","mtDevise","tauxChange","mtHtMad","tauxTva"].every(field=>Number.isFinite(line[field])))throw invalid();return {id:line.keyLigne,label:line.libelleRubrique||"",nature,gross:line.mtTotalDevise,discount:line.tauxRemise,net:line.mtDevise,rate:line.tauxChange,ht:line.mtHtMad,vatRate:line.tauxTva*100,vat:null};}),
 recap:row.tva.map((line,index)=>{if(typeof line.codeTva!=="string"||!["ht","tva","mtImpute"].every(field=>Number.isFinite(line[field])))throw invalid();return {id:`tva-${index}`,rate:line.codeTva,ht:line.ht,vat:line.tva};})};};
export function createInvoicesService({request,timeoutMs=15000}){
 const path=id=>`/api/v1/factures-vente/${key(id)}`;
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
        throw new InvoicesApiError(rule || (response.status === 404 ? "missing" : response.status === 409 ? "conflict" : response.status === 400 || response.status === 422 ? "validation" : response.status === 403 ? "forbidden" : "unavailable"),
          rule ? rule : response.status === 404 ? "Cette facture ou cette référence n’est plus disponible. Actualisez la liste." : response.status === 409 ? "Ce numéro de facture est déjà utilisé." : response.status === 400 || response.status === 422 ? "Les données ont été refusées par le serveur. Vérifiez les champs et les règles métier." : response.status === 403 ? "Vous n’avez pas l’autorisation d’effectuer cette action." : "Le service Factures est indisponible. Réessayez.");
      }
      if (response.status !== expected) throw invalid();
      if (expected === 204) return null;
      const text = await response.text(); if (!text.trim()) throw invalid();
      try { return JSON.parse(text); } catch { throw invalid(); }
    } catch (error) {
      if (signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (error instanceof AuthenticationError) throw new InvoicesApiError(error.code, error.message);
      if (error instanceof InvoicesApiError && !(writing && error.code === "invalid_response")) throw error;
      if (writing) throw uncertain();
      if (timedOut) throw new InvoicesApiError("timeout", "Le serveur met trop de temps à répondre. Réessayez.");
      throw new InvoicesApiError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    } finally { clearTimeout(timer); signal?.removeEventListener("abort", abort); }
  };

 const get=async(id,{signal}={})=>{const row=detail(await call(path(id),{signal}));if(row.id!==id)throw invalid();return row;};
 const mutate=async(id,suffix,body,signal,expected=200,newKey=false)=>{const result=await call(id?path(id)+suffix:"/api/v1/factures-vente",{method:"POST",body,signal,expected});let row;try{row=detail(result);if(id&&!newKey&&row.id!==id)throw invalid();}catch{throw uncertain();}return {updatedId:row.id,row};};
 const paged=async(url,signal,page,pageSize)=>{const result=await call(url,{signal});if(!result||!Array.isArray(result.items)||result.page!==page||result.pageSize!==pageSize||!Number.isSafeInteger(result.totalCount)||result.totalCount<result.items.length||result.items.length>pageSize)throw invalid();return result;};
const currencies=async({signal}={})=>{const rows=await call("/api/v1/reference/devises",{signal});if(!Array.isArray(rows)||rows.some(row=>typeof row.codeDevise!=="string"||!row.codeDevise))throw invalid();return rows.map(row=>row.codeDevise);};
 return {serverPaging:true,get,
 async list({search="",client="",type="",status="",settled="",from="",to="",page=1,size=5,signal}={}){if(!Number.isInteger(page)||page<1||![5,10,20].includes(size))throw invalid();if(from)date(from);if(to)date(to);if(from&&to&&from>to)throw new InvoiceError("La date de fin doit être postérieure ou égale à la date de début.");if(settled!==""&&settled!=="true"&&settled!=="false")throw invalid();if(type)kind(type);
 const params=new URLSearchParams({page:String(page),pageSize:String(size)});if(search.trim())params.set("search",search.trim());if(client)params.set("keyClient",client);if(type)params.set("type",String(invoiceTypes[type]));if(status)params.set("statut",status);if(settled!=="")params.set("factRegle",settled);if(from)params.set("from",date(from));if(to)params.set("to",to+"T23:59:59.9999999");const result=await paged(`/api/v1/factures-vente?${params}`,signal,page,size);return {...result,invoices:result.items.map(summary),clients:[]};},
 async clients({search="",page=1,signal}={}){const params=new URLSearchParams({page:String(page),pageSize:"20"});if(search.trim())params.set("search",search.trim());const result=await paged(`/api/v1/clients?${params}`,signal,page,20);return {...result,items:result.items.map(row=>{if(typeof row.keyClient!=="string")throw invalid();return {id:row.keyClient,label:row.raisonSociale||row.keyClient};})};},
 currencies,
 async create(value,{signal}={}){const codes=await currencies({signal});validateInvoice(value,[{id:value.clientId}],codes);if(!codes.includes(value.currency))throw new InvoiceError("Sélectionnez une devise disponible.");if(!value.clientId?.trim())throw new InvoiceError("Sélectionnez un client.");const client=await call(`/api/v1/clients/${key(value.clientId)}`,{signal});if(client?.keyClient!==value.clientId||typeof client.raisonSociale!=="string")throw invalid();
 const body={keyClient:value.clientId,intituleClient:client.raisonSociale,deviseFacture:value.currency,tauxDeviseFacture:Number(value.rate),dateFacture:date(value.date),dateEcheance:date(value.dueDate),numFacture:value.number.trim()||null,keyDossier:value.dossier?.trim()||null,keyVoyageInt:value.voyage?.trim()||null,natureService:value.service?.trim()||null,commentaireFacture:value.comment?.trim()||null,lignes:value.lines.map(line=>({libelleRubrique:line.label.trim(),natureLigne:natures.indexOf(line.nature),mtTotalDevise:Number(line.gross),tauxRemise:Number(line.discount),tauxChange:Number(line.rate),tauxTva:Number(line.vatRate)/100}))};return mutate(null,"",body,signal,201);},
 recalculate:(id,{signal}={})=>mutate(id,"/recalculate",undefined,signal),
 credit:(id,value,{signal}={})=>{if(typeof value.settled!=="boolean")throw new InvoiceError("Vérifiez l’état de règlement de l’avoir.");return mutate(id,`/avoir-total?${new URLSearchParams({avoirTotalFactRegle:String(value.settled)})}`,undefined,signal,201,true);},
 exempt:(id,value,{signal}={})=>mutate(id,"/exoneration"+(value.comment?.trim()?"?"+new URLSearchParams({commentaire:value.comment.trim()}):""),undefined,signal)
 };
}

