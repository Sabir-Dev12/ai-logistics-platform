import { AuthenticationError } from "./authenticationService";
import { documentTypes, documentStatuses, fileType, validateDocumentBatch } from "../KnowledgeBase/documentTypes";
export class KnowledgeBaseApiError extends Error {
 constructor(code,message,{uncertain=false}={}){super(message);this.name="KnowledgeBaseApiError";this.code=code;this.uncertain=uncertain;}
}
const invalid=()=>new KnowledgeBaseApiError("invalid_response","La réponse documentaire est invalide. Actualisez la liste.");
const uncertain=()=>new KnowledgeBaseApiError("uncertain","La confirmation du serveur n’a pas été reçue. Actualisez puis vérifiez les documents avant de recommencer.",{uncertain:true});
function document(raw){
 if(!raw||typeof raw.id!=="string"||!raw.id.trim()||typeof raw.name!=="string"||!raw.name.trim()||!documentTypes.includes(raw.type)||!Object.hasOwn(documentStatuses,raw.status)||!Number.isSafeInteger(raw.size)||raw.size<0||typeof raw.date!=="string"||!Number.isFinite(Date.parse(raw.date))||!Number.isSafeInteger(raw.chunkCount)||raw.chunkCount<0||raw.modelId!=null&&typeof raw.modelId!=="string"||raw.errorCode!=null&&typeof raw.errorCode!=="string")throw invalid();
 return {id:raw.id,name:raw.name,type:raw.type,size:raw.size,date:raw.date,status:raw.status,chunkCount:raw.chunkCount,modelId:raw.modelId??null,errorCode:raw.errorCode??null};
}
export function createKnowledgeBaseService({request,timeoutMs=15000,uploadTimeoutMs=120000}){
 const base="/api/v1/ai/documents";
 async function call(url,{signal,method="GET",body,expected=200}={}){
  const controller=new AbortController(),abort=()=>controller.abort();
  if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
  let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},method==="POST"?uploadTimeoutMs:timeoutMs),writing=method!=="GET";
  try{
   const response=await request(url,{method,signal:controller.signal,headers:{Accept:"application/json"},...(body===undefined?{}:{body})});
   if(!response.ok){
    if(writing&&response.status>=500)throw uncertain();
    const code=({401:"unauthorized",403:"forbidden",404:"missing",400:"validation",413:"size",422:"validation",409:"conflict",429:"rate_limit"})[response.status]||"unavailable";
    const message=({unauthorized:"Votre session n’est plus autorisée. Reconnectez-vous.",forbidden:"Vous n’avez pas accès à cette action.",missing:"Ce document n’est plus disponible. Actualisez la liste.",validation:"Les documents ou filtres ont été refusés. Vérifiez les formats, limites et champs.",size:"Le serveur refuse la taille de cet envoi. Réduisez le lot.",conflict:"Cette opération entre en conflit avec les documents actuels.",rate_limit:"Trop de demandes. Patientez puis réessayez."})[code]||"Le service documentaire est indisponible. Réessayez.";
    throw new KnowledgeBaseApiError(code,message);
   }
   if(response.status!==expected)throw invalid();
   if(expected===204)return null;
   try{return JSON.parse(await response.text());}catch{throw invalid();}
  }catch(error){
   if(signal?.aborted)throw new DOMException("Annulé","AbortError");
   if(error instanceof AuthenticationError){if(writing&&!["unauthorized","forbidden","invalid_target"].includes(error.code))throw uncertain();throw new KnowledgeBaseApiError(error.code,error.message);}
   if(error instanceof KnowledgeBaseApiError&&!(writing&&error.code==="invalid_response"))throw error;
   if(writing)throw uncertain();
   throw new KnowledgeBaseApiError(timedOut?"timeout":"network",timedOut?"Le serveur met trop de temps à répondre. Réessayez.":"Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
  }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
 }
 return {serverPaging:true,
  async list({query="",type="",status="",page=1,pageSize=5,signal}={}){
   const params=new URLSearchParams({page:String(page),pageSize:String(pageSize)});
   if(query.trim())params.set("search",query.trim());if(type)params.set("type",type);if(status)params.set("status",status);
   const raw=await call(`${base}?${params}`,{signal});
   if(!raw||!Array.isArray(raw.items)||raw.page!==page||raw.pageSize!==pageSize||!Number.isSafeInteger(raw.totalCount)||raw.totalCount<0||raw.items.length>pageSize||raw.items.length>raw.totalCount)throw invalid();
   const documents=raw.items.map(document);if(new Set(documents.map(row=>row.id)).size!==documents.length)throw invalid();
   return {documents,totalCount:raw.totalCount};
  },
  async upload(files,{signal}={}){
   const error=validateDocumentBatch(files);if(error)throw new KnowledgeBaseApiError("validation",error);
   const body=new FormData();for(const file of files)body.append("files",file);
   const raw=await call(base,{method:"POST",body,signal,expected:201});
   try{
    if(!Array.isArray(raw)||raw.length!==files.length)throw invalid();const rows=raw.map(document);
    if(new Set(rows.map(row=>row.id)).size!==rows.length||rows.some((row,index)=>row.name!==files[index].name||row.size!==files[index].size||row.type!==fileType(files[index].name)))throw invalid();
    return rows;
   }catch{throw uncertain();}
  },
  async remove(id,{signal}={}){if(typeof id!=="string"||!id.trim())throw new KnowledgeBaseApiError("validation","Sélectionnez un document valide.");await call(`${base}/${encodeURIComponent(id)}`,{method:"DELETE",signal,expected:204});}
 };
}
