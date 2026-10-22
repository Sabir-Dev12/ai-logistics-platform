import {AuthenticationError} from "./authenticationService";
import {EdiError} from "../Edi/mockEdiRepository";
export class EdiApiError extends Error{constructor(code,message,{uncertain=false}={}){super(message);this.name="EdiApiError";this.code=code;this.uncertain=uncertain;}}
const invalid=()=>new EdiApiError("invalid_response","La réponse EDI est invalide. Vérifiez la version du backend.");
const uncertain=()=>new EdiApiError("uncertain","La confirmation d’une écriture EDI n’a pas été reçue. Vérifiez l’état serveur avant toute nouvelle opération.",{uncertain:true});
const rules={"edi.fichier":"Aucun fichier valide n’a été fourni.","edi.promotion.vide":"Aucune ligne en attente d’intégration.","edi.promotion.anomalies":"Des anomalies subsistent : corrigez-les avant de créer les voyages et dossiers."};
const count=n=>Number.isSafeInteger(n)&&n>=0;
const strings=rows=>Array.isArray(rows)&&rows.every(v=>typeof v==="string");
const base=()=>({analysis:null,stage:null,report:null,references:{},aliases:{},createdVoyages:[],createdDossiers:[],promotion:null});
const errors=rows=>{if(!Array.isArray(rows)||rows.some(r=>!Number.isSafeInteger(r.numeroLigne)||typeof r.message!=="string"))throw invalid();return rows.map((r,i)=>({id:`file-${i}`,line:r.numeroLigne,message:r.message}));};
function analysisDto(raw){
 if(!raw||typeof raw.fichierAccepte!=="boolean"||!count(raw.nbLignes)||!Array.isArray(raw.entetes)||!Array.isArray(raw.dossiers))throw invalid();
 const failures=errors(raw.erreursFichier),anomalies=[];
 const add=(row,type,reference)=>{if(!Number.isSafeInteger(row.numeroLigne)||!strings(row.anomalies))throw invalid();row.anomalies.forEach((message,i)=>anomalies.push({id:`${type}-${row.numeroLigne}-${i}`,line:row.numeroLigne,type,reference,message}));};
 const voyages=raw.entetes.map(r=>{if(typeof r.numVoyage!=="string")throw invalid();add(r,"Voyage",r.numVoyage);return {id:`header-${r.numeroLigne}`,line:r.numeroLigne,reference:r.numVoyage,departurePort:r.portDepart,arrivalPort:r.portArrive,departureDate:r.dateDepart?.slice(0,10)||"",arrivalDate:r.dateArrivee?.slice(0,10)||"",count:r.nbDossiers};});
 const dossiers=raw.dossiers.map(r=>{if(typeof r.numVoyage!=="string")throw invalid();add(r,"Dossier",r.numDossier||r.numVoyage);return {id:`detail-${r.numeroLigne}`,line:r.numeroLigne,reference:r.numDossier||"—",client:r.codeDestinataire,packages:r.colis,weight:r.poids,volume:r.volume,incoterm:r.incoterm,currency:r.devise};});
 if(!raw.fichierAccepte&&(voyages.length||dossiers.length))throw invalid();
 return {accepted:raw.fichierAccepte,lineCount:raw.nbLignes,errors:failures,anomalies,voyages,dossiers};
}
const stagedAnomalies=rows=>{if(!Array.isArray(rows))throw invalid();return rows.flatMap((r,i)=>{if(!count(r.numeroLigne)||typeof r.type!=="string"||typeof r.reference!=="string"||!strings(r.messages))throw invalid();return r.messages.map((message,j)=>({id:`staged-${i}-${j}`,line:r.numeroLigne,type:r.type,reference:r.reference,message}));});};
const reportAnomalies=rows=>{if(!Array.isArray(rows))throw invalid();return rows.map((r,i)=>{if(["type","reference","code","message"].some(k=>typeof r[k]!=="string"))throw invalid();return {id:`report-${i}`,line:"—",type:r.type,reference:r.reference,code:r.code,message:r.message};});};
export function createEdiService({request,timeoutMs=30000}){
 let state=base(),analyzedFile=null,sequence=0,blocked=false;
 const snapshot=notice=>({...state,notice});
  const call=async(url,{signal,method="GET",body,expected=200,readOnly=false}={})=>{
    const controller=new AbortController(),abort=()=>controller.abort();
    if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
    let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},timeoutMs),writing=method!=="GET"&&!readOnly;
    try{
      const response=await request(url,{method,signal:controller.signal,headers:{Accept:"application/json"},...(body===undefined?{}:{body})});
      if(!response.ok){
        if(writing&&response.status>=500)throw uncertain();
        let problem;try{problem=await response.json();}catch{problem={};}
        throw new EdiApiError(response.status===404?"missing":response.status===400||response.status===422?"validation":response.status===403?"forbidden":response.status===409?"conflict":"unavailable",
          rules[problem?.rule]||(response.status===404?"Cette préparation ou cette référence n’est plus disponible. Actualisez la liste.":response.status===400||response.status===422?"Les données ont été refusées. Vérifiez les champs et les règles métier.":response.status===403?"Vous n’avez pas l’autorisation d’effectuer cette action.":response.status===409?"Cette opération entre en conflit avec les données actuelles.":"Le service EDI est indisponible. Réessayez."));
      }
      if(response.status!==expected)throw invalid();
      try{return JSON.parse(await response.text());}catch{throw invalid();}
    }catch(error){
      if(signal?.aborted)throw new DOMException("Annulé","AbortError");
      if(error instanceof AuthenticationError)throw new EdiApiError(error.code,error.message);
      if(error instanceof EdiApiError&&!(writing&&error.code==="invalid_response"))throw error;
      if(writing)throw uncertain();
      throw new EdiApiError(timedOut?"timeout":"network",timedOut?"Le serveur met trop de temps à répondre. Réessayez.":"Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
  };
 const upload=file=>{if(!file||typeof file.name!=="string"||! /\.(stc|edi|txt)$/i.test(file.name)||!Number.isFinite(file.size)||file.size<=0||file.size>2*1024*1024)throw new EdiError("Sélectionnez un fichier STC, EDI ou TXT non vide, de 2 Mo maximum.");const form=new FormData();form.append("fichier",file,file.name);return form;};
 const active=()=>{if(blocked)throw uncertain();if(!state.analysis?.accepted||!state.stage||state.stage.analysisId!==state.analysis.id||state.stage.promoted)throw new EdiError("Importez l’analyse courante en préparation avant cette opération.");};
 const write=async(endpoint,body,signal,decode)=>{if(blocked)throw uncertain();try{const raw=await call(`/api/v1/edi/stc/${endpoint}`,{method:"POST",body,signal});try{return decode(raw);}catch(error){if(error instanceof EdiApiError&&error.code==="rejected")throw error;throw uncertain();}}catch(error){if(error.uncertain)blocked=true;throw error;}};
 return {remote:true,
 async list(){return snapshot();},
 async analyze(selection,{signal}={}){const form=upload(selection?.file);state={...state,analysis:null,report:null};analyzedFile=null;
  const parsed=analysisDto(await call("/api/v1/edi/stc/analyser",{method:"POST",body:form,signal,readOnly:true}));
  if(signal?.aborted)throw new DOMException("Annulé","AbortError");
  analyzedFile=selection.file;state={...state,analysis:{...parsed,id:`analysis-${++sequence}`,selectionId:selection.selectionId,name:selection.file.name},report:null};return snapshot("Analyse serveur terminée.");
 },
 async stage({signal}={}){if(blocked)throw uncertain();if(!state.analysis?.accepted||!analyzedFile)throw new EdiError("Analysez un fichier accepté avant de l’importer.");
  const stage=await write("importer",upload(analyzedFile),signal,raw=>{if(raw?.fichierAccepte===false)throw new EdiApiError("rejected","Le serveur a rejeté le fichier ; relancez son analyse.");if(raw?.fichierAccepte!==true||![raw.nbLignes,raw.nbEntetes,raw.nbDossiers,raw.nbAnomalies].every(count)||raw.nbEntetes!==state.analysis.voyages.length||raw.nbDossiers!==state.analysis.dossiers.length||raw.nbLignes!==state.analysis.lineCount)throw invalid();errors(raw.erreursFichier);return {analysisId:state.analysis.id,name:state.analysis.name,voyages:state.analysis.voyages,dossiers:state.analysis.dossiers,anomalies:stagedAnomalies(raw.anomalies),promoted:false};});
  state={...state,stage,report:null,promotion:null};return snapshot("Préparation confirmée par le serveur.");
 },
 async reconcile({signal}={}){active();state={...state,report:null};const report=await write("reconcilier",undefined,signal,raw=>{if(!raw||typeof raw.promouvable!=="boolean"||raw.nbVoyages!==state.stage.voyages.length||raw.nbDossiers!==state.stage.dossiers.length)throw invalid();const anomalies=reportAnomalies(raw.anomalies);if(raw.promouvable&&anomalies.length)throw invalid();return {valid:raw.promouvable&&raw.nbVoyages>0,anomalies};});state={...state,report};return snapshot(report.valid?"Réconciliation confirmée, aucune anomalie restante.":"Réconciliation terminée : des anomalies restent à résoudre.");},
 async promote({signal}={}){active();if(!state.report?.valid)throw new EdiError("Une réconciliation valide est obligatoire avant la création.");
  const promotion=await write("promouvoir",undefined,signal,raw=>{if(!raw||!count(raw.nbVoyagesCrees)||!count(raw.nbDossiersCrees)||!strings(raw.referencesVoyages)||raw.nbVoyagesCrees!==state.stage.voyages.length||raw.nbDossiersCrees!==state.stage.dossiers.length||raw.referencesVoyages.length!==raw.nbVoyagesCrees)throw invalid();return raw;});
  state={...state,stage:{...state.stage,promoted:true},promotion};return snapshot("Création des voyages et dossiers confirmée par le serveur.");
 }
 };
}
