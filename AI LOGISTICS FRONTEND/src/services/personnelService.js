import {AuthenticationError} from "./authenticationService";
import {PersonnelError} from "../Personnel/personnelRules";

export class PersonnelApiError extends Error {
  constructor(code,message,{uncertain=false}={}) {super(message);this.name="PersonnelApiError";this.code=code;this.uncertain=uncertain;}
}
const invalid=()=>new PersonnelApiError("invalid_response","La réponse Personnel est invalide. Vérifiez la version du backend puis actualisez.");
const uncertain=()=>new PersonnelApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez la fiche avant de recommencer.",{uncertain:true});
const key=value=>{if(typeof value!=="string"||!value.trim())throw new PersonnelError("Renseignez une référence valide.");return encodeURIComponent(value);};
const text=value=>typeof value==="string"?value.trim():"";
const date=value=>{if(!/^\d{4}-\d{2}-\d{2}$/.test(value||"")||!Number.isFinite(Date.parse(value))||new Date(value+"T00:00:00Z").toISOString().slice(0,10)!==value)throw new PersonnelError("Renseignez une date valide.");return value+"T00:00:00";};
const rules={"personnel.nom":"Le nom est obligatoire.","personnel.embauche":"La date d’embauche ne peut pas être postérieure à la date de sortie.","personnel.sortie":"La date de sortie ne peut pas précéder l’embauche.","personnel.deja_sorti":"La sortie de cet employé est déjà enregistrée.","personnel.cout":"Le coût mensuel ne peut pas être négatif."};
function map(row,complete=false){
  if(!row||typeof row.keyPersonnel!=="string"||!row.keyPersonnel||typeof row.nomComplet!=="string"||typeof row.bloque!=="boolean"||typeof row.isActif!=="boolean"||typeof row.isChauffeur!=="boolean"||row.cout!=null&&(!Number.isFinite(row.cout)||row.cout<0)||row.dateSortie!=null&&typeof row.dateSortie!=="string"||row.dateEmbauche!=null&&typeof row.dateEmbauche!=="string")throw invalid();
  // Never split a compound full name to fabricate editable first/last names.
  if(complete&&(!Object.hasOwn(row,"nom")||!Object.hasOwn(row,"prenom")||!Object.hasOwn(row,"keyDepartement")))throw invalid();
  return {id:row.keyPersonnel,employeeNumber:row.matricule||"",lastName:row.nom??row.nomComplet,firstName:row.prenom||"",functionId:row.keyFonctionPersonnel||"",serviceId:row.keyServicePersonnel||"",departmentId:row.keyDepartement||"",agencyId:row.agencePersonnel||"",city:row.villePersonnel||"",mobile:row.gsmPersonnel||"",email:row.emailPersonnel||"",hireDate:row.dateEmbauche?.slice(0,10)||"",departureDate:row.dateSortie?.slice(0,10)||"",departureReason:row.motifSortie||"",blocked:row.bloque,identifier:row.identifiantApp||"",cost:row.cout??null,costCurrency:row.deviseCout||""};
}
export function createPersonnelService({request,timeoutMs=15000}){
  const path=id=>`/api/v1/resources/personnel/${key(id)}`;
  const call=async(url,{signal,method="GET",body,expected=200}={})=>{
    const controller=new AbortController(),abort=()=>controller.abort();
    if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
    let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},timeoutMs),writing=method!=="GET";
    try{
      const response=await request(url,{method,signal:controller.signal,headers:{Accept:"application/json",...(body===undefined?{}:{"Content-Type":"application/json"})},...(body===undefined?{}:{body:JSON.stringify(body)})});
      if(!response.ok){
        if(writing&&response.status>=500)throw uncertain();
        let problem;try{problem=await response.json();}catch{problem={};}
        throw new PersonnelApiError(response.status===404?"missing":response.status===400||response.status===422?"validation":response.status===403?"forbidden":response.status===409?"conflict":"unavailable",
          rules[problem?.rule]||(response.status===404?"Cet employé ou cette référence n’est plus disponible. Actualisez la liste.":response.status===400||response.status===422?"Les données ont été refusées. Vérifiez les champs et les règles métier.":response.status===403?"Vous n’avez pas l’autorisation d’effectuer cette action.":response.status===409?"Cette opération entre en conflit avec les données actuelles.":"Le service Personnel est indisponible. Réessayez."));
      }
      if(response.status!==expected)throw invalid();
      try{return JSON.parse(await response.text());}catch{throw invalid();}
    }catch(error){
      if(signal?.aborted)throw new DOMException("Annulé","AbortError");
      if(error instanceof AuthenticationError)throw new PersonnelApiError(error.code,error.message);
      if(error instanceof PersonnelApiError&&!(writing&&error.code==="invalid_response"))throw error;
      if(writing)throw uncertain();
      throw new PersonnelApiError(timedOut?"timeout":"network",timedOut?"Le serveur met trop de temps à répondre. Réessayez.":"Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
  };
  const get=async(id,{signal}={})=>{const row=map(await call(path(id),{signal}),true);if(row.id!==id)throw invalid();return row;};
  const currencies=async({signal}={})=>{const rows=await call("/api/v1/reference/devises",{signal});if(!Array.isArray(rows)||rows.some(row=>typeof row.codeDevise!=="string"||!row.codeDevise))throw invalid();return rows.map(row=>row.codeDevise);};
  const mutate=async(id,suffix,method,body,signal)=>{const result=await call(id?path(id)+suffix:"/api/v1/resources/personnel",{signal,method,body,expected:id?200:201});try{const row=map(result,true);if(id&&row.id!==id)throw invalid();return {updatedId:row.id,row};}catch{throw uncertain();}};
  const identity=value=>{if(!text(value.lastName))throw new PersonnelError("Le nom est obligatoire.");if(value.email&&!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.email))throw new PersonnelError("Renseignez une adresse email valide.");return {nom:text(value.lastName),prenom:text(value.firstName)||null,matricule:text(value.employeeNumber)||null,keyFonction:text(value.functionId)||null,keyService:text(value.serviceId)||null,agence:text(value.agencyId),ville:text(value.city),gsm:text(value.mobile)||null,email:text(value.email)||null,dateEmbauche:value.hireDate?date(value.hireDate):null};};
  return {serverPaging:true,get,currencies,
    async list({search="",status="active",functionId="",agencyId="",page=1,size=5,signal}={}){
      if(!Number.isSafeInteger(page)||page<1||![5,10,20].includes(size)||!["active","all"].includes(status))throw invalid();
      const params=new URLSearchParams({page:String(page),pageSize:String(size),includeSortis:String(status==="all")});
      if(search.trim())params.set("recherche",search.trim());if(functionId.trim())params.set("keyFonction",functionId.trim());if(agencyId)params.set("agence",agencyId);
      const [result,agencies]=await Promise.all([call(`/api/v1/resources/personnel?${params}`,{signal}),call("/api/v1/reference/agences",{signal})]);
      if(!result||!Array.isArray(result.items)||result.page!==page||result.pageSize!==size||!Number.isSafeInteger(result.totalCount)||result.totalCount<result.items.length||result.items.length>size||!Array.isArray(agencies)||agencies.some(row=>typeof row.keyAgence!=="string"))throw invalid();
      return {employees:result.items.map(row=>map(row)),totalCount:result.totalCount,functions:[],services:[],agencies:agencies.map(row=>({id:row.keyAgence,label:row.intituleAgence||row.keyAgence}))};
    },
    create:async(value,{signal}={})=>mutate(null,"","POST",identity(value),signal),
    async update(id,value,{signal}={}){const body=identity(value),current=await get(id,{signal});if(value.hireDate&&current.departureDate&&value.hireDate>current.departureDate)throw new PersonnelError("La date d’embauche ne peut pas être postérieure à la date de sortie.");return mutate(id,"","PUT",{...body,keyDepartement:current.departmentId||null},signal);},
    async depart(id,value,{signal}={}){const departure=date(value.date),current=await get(id,{signal});if(current.departureDate)throw new PersonnelError("La sortie de cet employé est déjà enregistrée.");if(current.hireDate&&value.date<current.hireDate)throw new PersonnelError("La date de sortie ne peut pas précéder l’embauche.");return mutate(id,"/sortie","POST",{dateSortie:departure,motif:text(value.reason)||null},signal);},
    async setCost(id,value,{signal}={}){const cost=value.cost===""||value.cost==null?null:Number(value.cost);if(cost!==null&&(!Number.isFinite(cost)||cost<0||cost>999999999999.99))throw new PersonnelError("Le coût mensuel doit être positif ou nul et dans la plage autorisée.");if(cost!==null&&!(await currencies({signal})).includes(value.currency))throw new PersonnelError("Sélectionnez une devise existante.");return mutate(id,"/cout","PUT",{cout:cost,deviseCout:cost===null?null:value.currency},signal);}
  };
}
