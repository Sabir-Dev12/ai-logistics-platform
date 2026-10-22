import {AuthenticationError} from "./authenticationService";
import {EquipmentError,retired} from "../Equipment/equipmentRules";
export class EquipmentApiError extends Error{constructor(code,message,{uncertain=false}={}){super(message);this.name="EquipmentApiError";this.code=code;this.uncertain=uncertain;}}
const invalid=()=>new EquipmentApiError("invalid_response","La réponse Matériel est invalide. Vérifiez la version du backend puis actualisez.");
const uncertain=()=>new EquipmentApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez la fiche matériel avant de recommencer.",{uncertain:true});
const key=value=>{if(typeof value!=="string"||!value.trim())throw new EquipmentError("Renseignez une référence valide.");return encodeURIComponent(value);};
const text=value=>typeof value==="string"?value.trim():"";
const date=value=>{if(!/^\d{4}-\d{2}-\d{2}$/.test(value||"")||!Number.isFinite(Date.parse(value))||new Date(value+"T00:00:00Z").toISOString().slice(0,10)!==value)throw new EquipmentError("Renseignez une date valide.");return value+"T00:00:00";};
const optionalNumber=value=>{if(value===""||value==null)return null;const n=Number(value);if(!Number.isFinite(n)||n<0||n>999999999999.99)throw new EquipmentError("Le montant et le kilométrage doivent être positifs ou nuls et dans la plage autorisée.");return n;};
const statuses=["Disponible","EnReparation","Sorti"];
const rules={"materiel.type":"Le type de matériel est obligatoire.","materiel.dates":"La mise en circulation ne peut pas précéder l’acquisition.","materiel.sortie_date":"La sortie ne peut pas précéder l’acquisition.","materiel.sortie_valeur":"Le montant et le kilométrage doivent être positifs ou nuls.","materiel.deja_sorti":"Ce matériel est déjà sorti du parc.","materiel.retire":"Un matériel sorti du parc ne peut pas être mis en réparation.","materiel.reparation":"Ce matériel n’est pas en réparation.","materiel.cout":"Le coût mensuel ne peut pas être négatif."};
function map(row,complete=false){
 if(!row||typeof row.keyMateriel!=="string"||!row.keyMateriel||!statuses.includes(row.statut)||typeof row.supprimer!=="boolean"||typeof row.isRetire!=="boolean"||row.cout!=null&&(!Number.isFinite(row.cout)||row.cout<0)||["dateAcquisition","dateMiseCirculation","dateSortie","dateFinGarantie"].some(k=>row[k]!=null&&typeof row[k]!=="string"))throw invalid();
 if(complete&&(!Object.hasOwn(row,"moyenPropre")||!Object.hasOwn(row,"dateFinGarantie")))throw invalid();
 return {id:row.keyMateriel,registration:row.immatriculation||"",label:row.intitule||"",type:row.typeMateriel||"",nature:row.natureMateriel||"",status:row.statut,ownership:row.moyenPropre||"",acquisitionDate:row.dateAcquisition?.slice(0,10)||"",circulationDate:row.dateMiseCirculation?.slice(0,10)||"",warrantyDate:row.dateFinGarantie?.slice(0,10)||"",exitDate:row.dateSortie?.slice(0,10)||"",exitReason:row.natureSortie||"",saleAmount:row.montantVente??null,exitMileage:row.kmSortie??null,cost:row.cout??null,costCurrency:row.deviseCout||"",deleted:row.supprimer};
}
export function createEquipmentService({request,timeoutMs=15000}){
 const path=id=>`/api/v1/resources/materiel/${key(id)}`;
  const call=async(url,{signal,method="GET",body,expected=200}={})=>{
    const controller=new AbortController(),abort=()=>controller.abort();
    if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
    let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},timeoutMs),writing=method!=="GET";
    try{
      const response=await request(url,{method,signal:controller.signal,headers:{Accept:"application/json",...(body===undefined?{}:{"Content-Type":"application/json"})},...(body===undefined?{}:{body:JSON.stringify(body)})});
      if(!response.ok){
        if(writing&&response.status>=500)throw uncertain();
        let problem;try{problem=await response.json();}catch{problem={};}
        throw new EquipmentApiError(response.status===404?"missing":response.status===400||response.status===422?"validation":response.status===403?"forbidden":response.status===409?"conflict":"unavailable",
          rules[problem?.rule]||(response.status===404?"Ce matériel ou cette référence n’est plus disponible. Actualisez la liste.":response.status===400||response.status===422?"Les données ont été refusées. Vérifiez les champs et les règles métier.":response.status===403?"Vous n’avez pas l’autorisation d’effectuer cette action.":response.status===409?"Cette opération entre en conflit avec les données actuelles.":"Le service Matériel est indisponible. Réessayez."));
      }
      if(response.status!==expected)throw invalid();
      try{return JSON.parse(await response.text());}catch{throw invalid();}
    }catch(error){
      if(signal?.aborted)throw new DOMException("Annulé","AbortError");
      if(error instanceof AuthenticationError)throw new EquipmentApiError(error.code,error.message);
      if(error instanceof EquipmentApiError&&!(writing&&error.code==="invalid_response"))throw error;
      if(writing)throw uncertain();
      throw new EquipmentApiError(timedOut?"timeout":"network",timedOut?"Le serveur met trop de temps à répondre. Réessayez.":"Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
  };
 const get=async(id,{signal}={})=>{const row=map(await call(path(id),{signal}),true);if(row.id!==id||row.deleted)throw invalid();return row;};
 const currencies=async({signal}={})=>{const rows=await call("/api/v1/reference/devises",{signal});if(!Array.isArray(rows)||rows.some(r=>typeof r.codeDevise!=="string"||!r.codeDevise))throw invalid();return rows.map(r=>r.codeDevise);};
 const mutate=async(id,suffix,method,body,signal)=>{const result=await call(id?path(id)+suffix:"/api/v1/resources/materiel",{signal,method,body,expected:id?200:201});try{const row=map(result,true);if(row.deleted||id&&row.id!==id)throw invalid();return {updatedId:row.id,row};}catch{throw uncertain();}};
 const identity=value=>{if(!text(value.type))throw new EquipmentError("Le type de matériel est obligatoire.");if(value.acquisitionDate&&value.circulationDate&&value.circulationDate<value.acquisitionDate)throw new EquipmentError("La mise en circulation ne peut pas précéder l’acquisition.");return {immatriculation:text(value.registration)||null,intitule:text(value.label)||null,typeMateriel:text(value.type),natureMateriel:text(value.nature)||null,moyenPropre:text(value.ownership)||null,dateAcquisition:value.acquisitionDate?date(value.acquisitionDate):null,dateMiseCirculation:value.circulationDate?date(value.circulationDate):null,dateFinGarantie:value.warrantyDate?date(value.warrantyDate):null};};
 const repair=async(id,enter,{signal}={})=>{const row=await get(id,{signal});if(retired(row)||row.status!==(enter?"Disponible":"EnReparation"))throw new EquipmentError(enter?"Seul un matériel disponible dans le parc peut entrer en réparation.":"Ce matériel n’est pas en réparation.");return mutate(id,`/reparation/${enter?"entree":"sortie"}`,"POST",undefined,signal);};
 return {serverPaging:true,get,currencies,
  async list({search="",status="fleet",type="",page=1,size=5,signal}={}){
   if(!Number.isSafeInteger(page)||page<1||![5,10,20].includes(size)||!["fleet","all",...statuses].includes(status))throw invalid();
   const params=new URLSearchParams({page:String(page),pageSize:String(size),includeRetires:String(status==="all"||status==="Sorti")});
   if(statuses.includes(status))params.set("statut",String(statuses.indexOf(status)));if(text(type))params.set("typeMateriel",text(type));if(text(search))params.set("recherche",text(search));
   const result=await call(`/api/v1/resources/materiel?${params}`,{signal});if(!result||!Array.isArray(result.items)||result.page!==page||result.pageSize!==size||!Number.isSafeInteger(result.totalCount)||result.totalCount<result.items.length||result.items.length>size)throw invalid();
   return {equipment:result.items.map(r=>map(r)),totalCount:result.totalCount,types:[]};
  },
  create:async(value,{signal}={})=>mutate(null,"","POST",identity(value),signal),
  async update(id,value,{signal}={}){const body=identity(value),row=await get(id,{signal});if(row.exitDate&&value.acquisitionDate&&value.acquisitionDate>row.exitDate)throw new EquipmentError("L’acquisition ne peut pas être postérieure à la sortie.");return mutate(id,"","PUT",body,signal);},
  async dispose(id,value,{signal}={}){const when=date(value.date),amount=optionalNumber(value.saleAmount),km=optionalNumber(value.exitMileage),row=await get(id,{signal});if(retired(row))throw new EquipmentError("Ce matériel est déjà sorti du parc.");if(row.acquisitionDate&&value.date<row.acquisitionDate)throw new EquipmentError("La sortie ne peut pas précéder l’acquisition.");return mutate(id,"/sortie","POST",{dateSortie:when,natureSortie:text(value.reason)||null,montantVente:amount,kmSortie:km},signal);},
  enterRepair:(id,options)=>repair(id,true,options),leaveRepair:(id,options)=>repair(id,false,options),
  async setCost(id,value,{signal}={}){const cost=optionalNumber(value.cost);if(cost!==null&&!(await currencies({signal})).includes(value.currency))throw new EquipmentError("Sélectionnez une devise existante.");return mutate(id,"/cout","PUT",{cout:cost,deviseCout:cost===null?null:value.currency},signal);}
 };
}
