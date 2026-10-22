import {AuthenticationError} from "./authenticationService";
import {ReferenceError} from "../ReferenceData/mockReferenceRepository";
import {validateReference} from "../ReferenceData/referenceCatalog";
export class ReferencesApiError extends Error{constructor(code,message,{uncertain=false}={}){super(message);this.name="ReferencesApiError";this.code=code;this.uncertain=uncertain;}}
const invalid=()=>new ReferencesApiError("invalid_response","La réponse Référentiels est invalide. Actualisez la rubrique.");
const uncertain=()=>new ReferencesApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez la référence avant de recommencer.",{uncertain:true});
const key=v=>{if(typeof v!=="string"||!v.trim())throw new ReferenceError("Renseignez une référence valide.");return encodeURIComponent(v);};
const text=v=>typeof v==="string"?v.trim():"";
const rules={"referentiel.reference":"Cette référence est encore utilisée. Renouvelez la vérification.","referentiel.inconnu":"Ce référentiel n’est pas pris en charge."};
const enumNames=["Pays","Devise","Ville","Port","Incoterm","ZonePays","Agence","ModePaiement","CompteTva","ClasseClient","StatutClient","Risque","SecteurActivite","GroupeSociete","Tresorie","NatureFraisBancaire","TypeIncidentPaiement"];
const configs={countries:["pays","Pays","codePays","intitulePays"],currencies:["devises","Devise","codeDevise","libelleDevise"],agencies:["agences","Agence","keyAgence","intituleAgence"],vat:["comptes-tva","CompteTva","codeTva","codeTva"],payments:["modes-paiement","ModePaiement","modePaiementCode","modePaiementCode"],treasuries:["tresoreries","Tresorie","keyTresorie","libelleTresorie"],dossierTypes:["types-dossier",null,"typeDossierCode","intituleType"],incoterms:["incoterms","Incoterm","incotermCode","libelleIncoterm"],clientClasses:["classes-client","ClasseClient","classeClientLabel","classeClientLabel"],clientStatuses:["statuts-client","StatutClient","statutClientLabel","statutClientLabel"],clientRisks:["risques","Risque","intituleRisque","intituleRisque"],settings:["parametres",null,"keyParametre","libelleParametre"]};
function mapped(kind,r){const config=configs[kind];if(!r||typeof r[config[2]]!=="string"||!r[config[2]])throw invalid();
 const row={id:r[config[2]],code:r[config[2]],label:r[config[3]]||r[config[2]]};
 const extra=kind==="countries"?{alpha2:r.alpha2,agreement:r.accord,zone:r.zonePaysKey}:kind==="currencies"?{invoiceLabel:r.libelleFacture,minorUnit:r.libelleCent,decimals:r.nbDecimal}:kind==="agencies"?{code:r.codeAgence||row.code,city:r.villeAgence,address:[r.adresse1Agence,r.adresse2Agence].filter(Boolean).join(", "),deleted:r.supprime}:kind==="vat"?{rate:r.valeur==null?null:r.valeur*100,account:r.vente?r.compteVente:r.compteAchat}:kind==="treasuries"?{nature:typeof r.typeTresorie==="number"?["Banque","Caisse"][r.typeTresorie]:r.typeTresorie}:kind==="dossierTypes"?{nature:r.natureService}:kind==="incoterms"?{edi:r.incotermEdi,deleted:r.supprime}:kind==="settings"?{value:r.valeurParametre,description:r.libelleParametre}:{};
 return {...row,...extra};
}
const base=()=>({entries:{},company:null,canCreate:false,canDelete:false,canCheck:false,information:null});
export function createReferencesService({request,timeoutMs=15000}){
 const checks=new Map();let sequence=0;
  const call=async(url,{signal,method="GET",body,expected=200}={})=>{
    const controller=new AbortController(),abort=()=>controller.abort();
    if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
    let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},timeoutMs),writing=method!=="GET";
    try{
      const response=await request(url,{method,signal:controller.signal,headers:{Accept:"application/json",...(body===undefined?{}:{"Content-Type":"application/json"})},...(body===undefined?{}:{body:JSON.stringify(body)})});
      if(!response.ok){
        if(writing&&response.status>=500)throw uncertain();
        let problem;try{problem=await response.json();}catch{problem={};}
        throw new ReferencesApiError(response.status===404?"missing":response.status===400||response.status===422?"validation":response.status===403?"forbidden":response.status===409?"conflict":"unavailable",
          rules[problem?.rule]||(response.status===404?"Cette référence n’est plus disponible. Actualisez la liste.":response.status===400||response.status===422?"Les données ont été refusées. Vérifiez les champs et les règles métier.":response.status===403?"Vous n’avez pas l’autorisation d’effectuer cette action.":response.status===409?"Cette opération entre en conflit avec les données actuelles.":"Le service Référentiels est indisponible. Réessayez."));
      }
      if(response.status!==expected)throw invalid();
      if(expected===204)return null;
      try{return JSON.parse(await response.text());}catch{throw invalid();}
    }catch(error){
      if(signal?.aborted)throw new DOMException("Annulé","AbortError");
      if(error instanceof AuthenticationError)throw new ReferencesApiError(error.code,error.message);
      if(error instanceof ReferencesApiError&&!(writing&&error.code==="invalid_response"))throw error;
      if(writing)throw uncertain();
      throw new ReferencesApiError(timedOut?"timeout":"network",timedOut?"Le serveur met trop de temps à répondre. Réessayez.":"Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
  };
 const admin=async signal=>{const identity=await call("/api/v1/auth/moi",{signal});if(!identity||typeof identity!=="object"||Array.isArray(identity))throw invalid();return identity.estAdmin===true;};
 const config=kind=>{const value=configs[kind];if(!value?.[1])throw new ReferenceError("Ce référentiel est en lecture seule ou indisponible.");return value;};
 const check=async(kind,id,{signal}={})=>{const c=config(kind),raw=await call(`/api/v1/reference/${c[1]}/${key(id)}/references`,{signal});if(!raw||raw.key!==id||!(raw.referential===c[1]||enumNames[raw.referential]===c[1])||typeof raw.canDelete!=="boolean"||!Array.isArray(raw.referencingTables)||raw.referencingTables.some(t=>typeof t!=="string")||raw.canDelete&&raw.referencingTables.length)throw invalid();const checkId=`reference-check-${++sequence}`;checks.set(checkId,{kind,id});return {kind,id,checkId,canDelete:raw.canDelete,references:raw.referencingTables.map(label=>({label})),reason:raw.blockingReason||(raw.canDelete?"Aucune utilisation signalée par le serveur.":"Cette référence est encore utilisée."),softDelete:["agencies","incoterms"].includes(kind)};};
 return {remote:true,check,
  async list({kind="countries",signal}={}){checks.clear();const result=base();
   if(["clientSectors","clientGroups"].includes(kind))return {...result,information:"Le backend n’expose pas d’API de liste pour cette rubrique. Aucune donnée fictive n’est affichée."};
   if(kind==="company"){let raw;try{raw=await call("/api/v1/reference/societe",{signal});}catch(error){if(error.code==="missing")return result;throw error;}if(!raw||typeof raw.intituleSoc!=="string")throw invalid();return {...result,company:{name:raw.intituleSoc,address:[raw.adresse1Soc,raw.adresse2Soc].filter(Boolean).join(", "),city:raw.villeSoc,country:raw.paysSoc,currency:raw.deviseSoc,email:raw.emailSoc,phone:raw.telSoc}};}
   const c=configs[kind];if(!c)throw invalid();const [raw,isAdmin]=await Promise.all([call(`/api/v1/reference/${c[0]}`,{signal}),c[1]?admin(signal):Promise.resolve(false)]);if(!Array.isArray(raw))throw invalid();const rows=raw.map(r=>mapped(kind,r));if(new Set(rows.map(r=>r.id)).size!==rows.length)throw invalid();
   return {...result,entries:{[kind]:rows},canCreate:["countries","currencies"].includes(kind),canCheck:Boolean(c[1]),canDelete:isAdmin};
  },
  async create(kind,value,{signal}={}){if(!["countries","currencies"].includes(kind))throw new ReferenceError("La création n’est pas autorisée pour cette rubrique.");const errors=validateReference(kind,value,true);if(Object.keys(errors).length)throw new ReferenceError(Object.values(errors)[0]);const code=text(value.code).toUpperCase(),label=text(value.label),body=kind==="countries"?{codePays:code,intitulePays:label,alpha2:text(value.alpha2).toUpperCase()||null,accord:text(value.agreement)||null,zonePays:text(value.zone)||null}:{codeDevise:code,libelleDevise:label,libelleFacture:text(value.invoiceLabel)||null,libelleCent:text(value.minorUnit)||null,nbDecimal:Number(value.decimals)};
   const raw=await call(`/api/v1/reference/${configs[kind][0]}`,{method:"POST",body,signal,expected:201});try{const row=mapped(kind,raw);if(row.id!==code)throw invalid();return {updatedId:row.id,row,kind};}catch{throw uncertain();}
  },
  async remove(kind,id,checkId,{signal}={}){const c=config(kind),previous=checks.get(checkId);if(previous?.kind!==kind||previous?.id!==id)throw new ReferenceError("Vérifiez les références avant de confirmer la suppression.");checks.delete(checkId);
   if(!await admin(signal))throw new ReferencesApiError("forbidden","Votre profil ne permet pas la suppression.");const fresh=await check(kind,id,{signal});checks.delete(fresh.checkId);if(!fresh.canDelete)throw new ReferenceError("Suppression refusée : cette référence est encore utilisée.");await call(`/api/v1/reference/${c[1]}/${key(id)}`,{method:"DELETE",signal,expected:204});return {removedId:id,kind};
  }
 };
}
