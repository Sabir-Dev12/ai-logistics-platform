import {AuthenticationError} from "./authenticationService";
import {FuelError} from "../Fuel/mockFuelRepository";
export class FuelApiError extends Error{constructor(code,message,{uncertain=false}={}){super(message);this.name="FuelApiError";this.code=code;this.uncertain=uncertain;}}
const invalid=()=>new FuelApiError("invalid_response","La réponse Carburant est invalide. Actualisez la page.");
const uncertain=()=>new FuelApiError("uncertain","La confirmation n’a pas été reçue. Vérifiez les données carburant avant de recommencer.",{uncertain:true});
const key=v=>{if(typeof v!=="string"||!v.trim())throw new FuelError("Renseignez une clé de référence existante.");return encodeURIComponent(v);};
const text=v=>typeof v==="string"?v.trim():"";
const number=(v,positive=false)=>{if(v===""||v==null||!Number.isFinite(Number(v))||(positive?Number(v)<=0:Number(v)<0)||Number(v)>=1e9)throw new FuelError("Renseignez un nombre positif ou nul valide (strictement positif pour les litres).");return Number(v);};
const optional=v=>v===""||v==null?null:number(v);
const date=v=>{if(!/^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2})?)?$/.test(v||"")||!Number.isFinite(Date.parse(v))||new Date(v.slice(0,10)+"T00:00:00Z").toISOString().slice(0,10)!==v.slice(0,10))throw new FuelError("Renseignez une date valide.");return v.length===10?v+"T00:00:00":v.length===16?v+":00":v;};
const product=v=>{if(!["Gasoil","AdBlue"].includes(v))throw new FuelError("Sélectionnez un produit valide.");return v==="Gasoil"?0:1;};
const rules={"citerne.intitule":"Le libellé de la citerne est obligatoire.","citerne.confreres":"Cette citerne n’est pas un stock réel.","ratio.produit":"Produit inconnu.","citerne.sortie.stock":"Stock insuffisant dans la citerne."};
const required=(row,id,fields=[])=>{if(!row||typeof row[id]!=="string"||!row[id]||fields.some(k=>!Number.isFinite(row[k])))throw invalid();};
const stamp=v=>{if(typeof v!=="string"||!Number.isFinite(Date.parse(v)))throw invalid();return v;};
const tank=row=>{required(row,"keyCiterne",["stock","pmp","valeurStock"]);product(row.typeCiterne);return {id:row.keyCiterne,label:row.intituleCiterne||row.keyCiterne,agency:row.agence,product:row.typeCiterne,stock:row.stock,pmp:row.pmp,stockValue:row.valeurStock,sentinel:row.estConfreres};};
const issue=row=>{required(row,"keySortie",["qteSortie"]);if(typeof row.supprime!=="boolean"||typeof row.keyCiterne!=="string")throw invalid();return {id:row.keySortie,tankId:row.keyCiterne,date:stamp(row.dateSortie),product:row.typeCiterne,quantity:row.qteSortie,unitPrice:row.prixUnitaire,amount:row.montantSortie,equipmentId:row.keyMateriel,driverId:row.keyChauffeur,voyage:row.keyVoyage,meter:row.compteur,full:row.plein,note:row.observation,cancelled:row.supprime};};
const supply=row=>{required(row,"keyApprovisionnement",["qteTotale","montantApprovisionnement"]);if(!Array.isArray(row.lignes)||row.annulable!==false)throw invalid();return {id:row.keyApprovisionnement,date:stamp(row.dateApprovisionnement),reference:row.numBl,supplierId:row.keyFournisseur,quantity:row.qteTotale,amount:row.montantApprovisionnement,lines:row.lignes.map(l=>{required({id:l.keyCiterne},"id");if(!Number.isFinite(l.qteCiterne)||!Number.isFinite(l.montantAlloue))throw invalid();return {tankId:l.keyCiterne,quantity:l.qteCiterne,amount:l.montantAlloue};})};};
const voucher=row=>{required(row,"keyBon",["qteBon"]);return {id:row.keyBon,date:stamp(row.dateBon),reference:row.numBon,supplierId:row.keyFournisseur,product:row.typeCiterne,quantity:row.qteBon,amount:row.montantBon,currency:row.deviseBon,equipmentId:row.keyMateriel,driverId:row.keyChauffeur,voyage:row.keyVoyage,meter:row.compteur,full:row.plein,note:row.observation};};
const gauge=row=>{required(row,"keyJauge",["ecart"]);return {id:row.keyJauge,tankId:row.keyCiterne,date:stamp(row.dateJauge),actual:row.qteStock,theoretical:row.qteStockTheorique,gap:row.ecart};};
const ratio=row=>{required(row,"keyLigne");if(typeof row.keyTracteur!=="string")throw invalid();return {id:row.keyLigne,equipmentId:row.keyTracteur,from:stamp(row.dateDebut),to:stamp(row.dateFin),distance:row.totalKm,litres:row.totalLitre,amount:row.totalMontant,consumption:row.ratioConsommation,costKm:row.coutKm};};
const base=()=>({tanks:[],receptions:[],supplies:[],vouchers:[],issues:[],movements:[],gauges:[],ratios:[],equipment:[],drivers:[],suppliers:[],totalCount:0,information:null});
export function createFuelService({request,timeoutMs=15000}){
  const call=async(url,{signal,method="GET",body,expected=200}={})=>{
    const controller=new AbortController(),abort=()=>controller.abort();
    if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
    let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},timeoutMs),writing=method!=="GET";
    try{
      const response=await request(url,{method,signal:controller.signal,headers:{Accept:"application/json",...(body===undefined?{}:{"Content-Type":"application/json"})},...(body===undefined?{}:{body:JSON.stringify(body)})});
      if(!response.ok){
        if(writing&&response.status>=500)throw uncertain();
        let problem;try{problem=await response.json();}catch{problem={};}
        throw new FuelApiError(response.status===404?"missing":response.status===400||response.status===422?"validation":response.status===403?"forbidden":response.status===409?"conflict":"unavailable",
          rules[problem?.rule]||(response.status===404?"Ce document carburant ou cette référence n’est plus disponible. Actualisez la liste.":response.status===400||response.status===422?"Les données ont été refusées. Vérifiez les champs et les règles métier.":response.status===403?"Vous n’avez pas l’autorisation d’effectuer cette action.":response.status===409?"Cette opération entre en conflit avec les données actuelles.":"Le service Carburant est indisponible. Réessayez."));
      }
      if(!(Array.isArray(expected)?expected:[expected]).includes(response.status))throw invalid();
      if(response.status===204)return null;
      try{return JSON.parse(await response.text());}catch{throw invalid();}
    }catch(error){
      if(signal?.aborted)throw new DOMException("Annulé","AbortError");
      if(error instanceof AuthenticationError)throw new FuelApiError(error.code,error.message);
      if(error instanceof FuelApiError&&!(writing&&error.code==="invalid_response"))throw error;
      if(writing)throw uncertain();
      throw new FuelApiError(timedOut?"timeout":"network",timedOut?"Le serveur met trop de temps à répondre. Réessayez.":"Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
    }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
  };
 const currencies=async({signal}={})=>{const rows=await call("/api/v1/reference/devises",{signal});if(!Array.isArray(rows)||rows.some(r=>typeof r.codeDevise!=="string"||!r.codeDevise))throw invalid();return rows.map(r=>r.codeDevise);};
 const confirmed=async(url,body,map,signal,expected=201,method="POST",id=null)=>{const value=await call(url,{method,body,signal,expected});try{const row=map(value);if(id&&row.id!==id)throw invalid();return {row,notice:"Opération confirmée par le serveur."};}catch{throw uncertain();}};
 const affectation=value=>{const meter=optional(value.meter);if(typeof value.full!=="boolean")throw new FuelError("Vérifiez le champ Plein.");if(value.full&&(!text(value.equipmentId)||meter==null))throw new FuelError("Un plein nécessite un matériel et un compteur.");return {keyVoyage:text(value.voyage)||null,keyMateriel:text(value.equipmentId)||null,keyChauffeur:text(value.driverId)||null,compteur:meter,plein:value.full,observation:text(value.note)||null};};
 return {serverPaging:true,currencies,
  async references(kind,{search="",page=1,signal}={}){
   if(kind!=="citernes"||!Number.isSafeInteger(page)||page<1)throw invalid();
   const params=new URLSearchParams({page:String(page),pageSize:"20"});if(text(search))params.set("recherche",text(search));
   const raw=await call(`/api/v1/carburant/citernes?${params}`,{signal});
   if(!raw||!Array.isArray(raw.items)||raw.page!==page||raw.pageSize!==20||!Number.isSafeInteger(raw.totalCount)||raw.totalCount<0||raw.items.length>20||raw.items.length>raw.totalCount)throw invalid();
   return {...raw,items:raw.items.map(row=>{const mapped=tank(row);return {keyCiterne:mapped.id,label:mapped.label};})};
  },
  async list({viewId="tanks",search="",product:filterProduct="",tankId="",equipmentId="",from="",to="",page=1,size=5,signal}={}){
   const result=base();if(!Number.isSafeInteger(page)||page<1||![5,10,20].includes(size))throw invalid();
   if(from&&to&&from>to)return result;
   if(["receptions","gauges","movements"].includes(viewId))return {...result,information: viewId==="movements"?"Le backend n’expose pas d’API de consultation du journal des mouvements. Aucun historique simulé n’est affiché.":"Le backend n’expose pas l’historique de cette rubrique. La saisie est disponible ; son résultat confirmé est affiché après enregistrement."};
   if(viewId==="ratios"){
    if(!text(equipmentId))return {...result,information:"Renseignez la clé d’un matériel pour consulter ses ratios. Le contrat ne retourne ni produit ni devise."};
    const raw=await call(`/api/v1/carburant/ratios/${key(equipmentId)}`,{signal});if(!Array.isArray(raw))throw invalid();
    const rows=raw.map(ratio).filter(r=>(!from||r.from.slice(0,10)>=from)&&(!to||r.to.slice(0,10)<=to));
    return {...result,ratios:rows.slice((page-1)*size,page*size),totalCount:rows.length,information:"Le contrat des ratios ne retourne ni produit ni devise ; ces valeurs ne sont pas déduites."};
   }
   const names={tanks:["citernes",tank],supplies:["approvisionnements",supply],vouchers:["bons",voucher],issues:["sorties",issue]};if(!names[viewId])throw invalid();
   const params=new URLSearchParams({page:String(page),pageSize:String(size)});
   if(viewId==="tanks"){if(text(search))params.set("recherche",text(search));if(filterProduct)params.set("type",String(product(filterProduct)));}
   else {if(from)params.set("du",date(from));if(to)params.set("au",date(to).slice(0,10)+"T23:59:59.9999999");if(viewId==="issues"&&text(tankId))params.set("keyCiterne",text(tankId));}
   const raw=await call(`/api/v1/carburant/${names[viewId][0]}?${params}`,{signal});if(!raw||!Array.isArray(raw.items)||raw.page!==page||raw.pageSize!==size||!Number.isSafeInteger(raw.totalCount)||raw.totalCount<raw.items.length||raw.items.length>size)throw invalid();
   return {...result,[viewId]:raw.items.map(names[viewId][1]),totalCount:raw.totalCount};
  },
  async save(kind,value,{signal}={}){
   const url="/api/v1/carburant/";
   if(kind==="tank"){if(!text(value.label))throw new FuelError("Le libellé de la citerne est obligatoire.");return confirmed(url+"citernes",{intituleCiterne:text(value.label),typeCiterne:product(value.product),agence:text(value.agency)||null},tank,signal);}
   if(kind==="reception"){const id=key(value.tankId),quantity=number(value.quantity,true),amount=number(value.amount);const result=await confirmed(url+`citernes/${id}/reception`,{quantite:quantity,montant:amount},tank,signal,200,"POST",value.tankId);return {...result,notice:`Réception confirmée. Stock serveur : ${result.row.stock} L ; PMP : ${result.row.pmp}.`};}
   if(kind==="gauge")return confirmed(url+`citernes/${key(value.tankId)}/jauge`,{quantiteReelle:number(value.quantity),dateJauge:date(value.date)},gauge,signal);
   if(kind==="issue")return confirmed(url+`citernes/${key(value.tankId)}/sorties`,{quantite:number(value.quantity,true),dateSortie:date(value.date),...affectation(value)},issue,signal);
   if(kind==="supply"){key(value.supplierId);if(!Array.isArray(value.lines)||!value.lines.length)throw new FuelError("Ajoutez au moins une ligne de citerne.");const lines=value.lines.map(l=>{key(l.tankId);return {keyCiterne:l.tankId,quantite:number(l.quantity,true)};});if(new Set(lines.map(l=>l.keyCiterne)).size!==lines.length)throw new FuelError("Une citerne ne peut apparaître qu’une fois dans le BL.");return confirmed(url+"approvisionnements",{keyFournisseur:value.supplierId,dateApprovisionnement:date(value.date),montant:number(value.amount),numBl:text(value.reference)||null,lignes:lines},supply,signal);}
   if(kind==="voucher"){key(value.supplierId);const body={keyFournisseur:value.supplierId,dateBon:date(value.date),quantite:number(value.quantity,true),numBon:text(value.reference)||null,devise:value.currency,montant:number(value.amount),typeCiterne:product(value.product),...affectation(value)};if(!(await currencies({signal})).includes(value.currency))throw new FuelError("Sélectionnez une devise existante.");return confirmed(url+"bons",body,voucher,signal);}
   if(kind==="cancel")return confirmed(url+`sorties/${key(value.id)}`,undefined,issue,signal,200,"DELETE",value.id);
   if(kind==="ratio"){const id=key(value.equipmentId);product(value.product);const raw=await call(url+`ratios/${id}?${new URLSearchParams({produit:value.product})}`,{method:"POST",signal,expected:[200,204]});if(raw===null)return {notice:"Aucun ratio calculable : une fenêtre valide entre deux pleins est nécessaire."};try{const row=ratio(raw);if(row.equipmentId!==value.equipmentId)throw invalid();return {row,notice:"Ratio confirmé par le serveur."};}catch{throw uncertain();}}
   throw new FuelError("Opération inconnue.");
  }
 };
}
