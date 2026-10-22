import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, EmptyState, ErrorState, Input, LoadingState, Table } from "../design-system";
import {useBookingResource} from "../Bookings/useBookingResource";
import { money, previewCosts, rates } from "./voyageRules";
export default function VoyageCosts(props){return props.repository?<ApiCosts {...props}/>:<LocalCosts {...props}/>;}
function LocalCosts({ row, dossiers, busy, onGenerate }) {
  const [month, setMonth] = useState(row.dateVoyage?.slice(0, 7) || new Date().toISOString().slice(0, 7));
  const preview = previewCosts(row, dossiers, month);
  return <div className="bookings-form"><Alert title="Coûts de démonstration">Taux et montants fictifs. La génération enregistre uniquement des répartitions en mémoire, sans créer de facture réelle.</Alert>
    <Table caption="Taux de coûts fictifs" rows={rates} columns={[{ key: "label", header: "Coût" }, { key: "value", header: "Taux", render: money }, { key: "unit", header: "Unité" }]} />
    <Input label="Mois de calcul" type="month" value={month} onChange={(event) => setMonth(event.target.value)} disabled={busy} />
    <div className="bookings-actions"><Badge variant={preview.eligible ? "success" : "warning"}>{preview.eligible ? "Éligible" : "Non éligible"}</Badge><span>{preview.nbDossiers} dossier(s) associé(s)</span></div>
    {!preview.eligible ? <Alert title="Aperçu indisponible" variant="warning">{preview.reason}</Alert> : <><p>Répartition égale entre les dossiers, arrondie à trois décimales.</p><Table caption="Aperçu des coûts à générer" rows={preview.allocations} rowKey="operation" columns={[{ key: "operation", header: "Opération" }, { key: "amount", header: "Total opération (MAD)", render: money }, { key: "perDossier", header: "Par dossier (MAD)", render: money }]} emptyState={<EmptyState title="Aucun nouveau coût à générer" />} /><p>Total des opérations à générer : {money(preview.total)} MAD</p></>}
    {preview.skipped.length > 0 && <Alert title="Opérations déjà générées">{preview.skipped.join(" · ")}</Alert>}
    <Button variant="primary" disabled={busy || !preview.eligible || !preview.allocations.length} onClick={() => onGenerate(month)}>Générer les coûts (démo)</Button>
    <Table caption="Coûts générés dans la démonstration" rows={row.generatedCosts} columns={[{ key: "dossier", header: "Dossier" }, { key: "operation", header: "Opération" }, { key: "montant", header: "Montant MAD", render: money }, { key: "month", header: "Mois" }]} emptyState={<EmptyState title="Aucun coût généré" />} />
  </div>;
}
VoyageCosts.propTypes = { row: PropTypes.object.isRequired, dossiers: PropTypes.array.isRequired, busy: PropTypes.bool,repository:PropTypes.object,blocked:PropTypes.bool,generated:PropTypes.object,onGenerate: PropTypes.func.isRequired };

const displayRate=value=>value==null?"Non résolu":money(value);
function ApiCosts({row,repository,busy,blocked,generated,onGenerate}){
 const [month,setMonth]=useState(row.dateVoyage?.slice(0,7)||new Date().toISOString().slice(0,7));
 const loadRates=useCallback(async signal=>{const [rates,operations]=await Promise.all([repository.rates(row.id,{signal}),repository.operations(row.id,{signal})]);return {rates,operations};},[repository,row.id]);
 const resource=useBookingResource(loadRates),validMonth=/^\d{4}-(0[1-9]|1[0-2])$/.test(month);
 const loadPreview=useCallback(async signal=>{if(resource.phase!=="ready"||!validMonth)return null;const rates=resource.value.rates;const request={moisCalcul:month,coutFraisBancaire:rates.coutFraisBancaire,legs:rates.legs,equipements:rates.equipements,personnel:rates.personnel};const result=await repository.preview(row.id,request,{signal});return {result,request};},[repository,row.id,resource.phase,resource.value,month,validMonth]);
 const preview=useBookingResource(loadPreview),result=preview.value?.result;
 return <div className="bookings-form"><Alert title="Coûts du voyage">Taux lus depuis le backend. La génération enregistre les répartitions ; elle ne crée pas de facture.</Alert>
 <Button disabled={busy} onClick={resource.retry}>Actualiser les taux</Button>
 {resource.phase==="loading"?<LoadingState label="Chargement des taux…"/>:resource.phase==="error"?<ErrorState message={resource.error} onRetry={resource.retry}/>:<>
 <Table caption="Taux de coûts du voyage" rowKey="id" rows={[["Société",resource.value.rates.coutSociete],["Assurance marchandise",resource.value.rates.coutAssuranceMarchandise],["Frais bancaires",resource.value.rates.coutFraisBancaire],["MEAD",resource.value.rates.coutMead],["Agence",resource.value.rates.coutAgence]].map(([label,value],index)=>({id:index,label,value}))} columns={[{key:"label",header:"Coût"},{key:"value",header:"Taux MAD",render:displayRate}]}/>
 <p>Diviseur des coûts mensuels : {resource.value.rates.nbTrajetsDiviseur}</p>
 {resource.value.rates.avertissements.length>0&&<Alert title="Taux incomplets" variant="warning">{resource.value.rates.avertissements.join(" · ")} La génération est bloquée jusqu’à résolution des taux.</Alert>}
 <Table caption="Taux des trajets" className="bookings-detail-table" rowKey="keyTrajet" rows={resource.value.rates.legs} columns={[{key:"keyTrajet",header:"Trajet"},{key:"kilometrage",header:"Km"},{key:"coutTracteurParTrajet",header:"Tracteur / trajet",render:displayRate},{key:"coutRemorqueParTrajet",header:"Remorque / trajet",render:displayRate},{key:"coutChauffeurParTrajet",header:"Chauffeur / trajet",render:displayRate},{key:"coutKmTracteur",header:"Tracteur / km",render:displayRate},{key:"coutKmGasoil",header:"Gasoil / km",render:displayRate},{key:"coutKmAdBlue",header:"AdBlue / km",render:displayRate}]} emptyState={<EmptyState title="Aucun taux de trajet"/>}/>
 <Table caption="Taux des équipements" rowKey="keyLigneEquipement" rows={resource.value.rates.equipements} columns={[{key:"typeEquipement",header:"Type"},{key:"qte",header:"Quantité"},{key:"coutParTrajet",header:"Coût / trajet",render:displayRate}]} emptyState={<EmptyState title="Aucun taux d’équipement"/>}/>
 <Table caption="Taux du personnel" rowKey="keyPersonnel" rows={resource.value.rates.personnel} columns={[{key:"keyPersonnel",header:"Personnel"},{key:"coutParTrajet",header:"Coût / trajet",render:displayRate}]} emptyState={<EmptyState title="Aucun taux de personnel"/>}/>
 <Table caption="Opérations de coûts" rowKey="operation" rows={resource.value.operations} columns={[{key:"operation",header:"Opération"},{key:"isCalculated",header:"État",render:value=><Badge variant={value?"success":"info"}>{value?"Déjà calculée":"À calculer"}</Badge>}]}/>
 </>}
 <Input label="Mois de calcul" type="month" value={month} onChange={event=>setMonth(event.target.value)} disabled={busy} error={!validMonth?"Sélectionnez un mois de calcul valide.":undefined}/>
 {validMonth&&resource.phase==="ready"&&(preview.phase==="loading"?<LoadingState label="Calcul de l’aperçu…"/>:preview.phase==="error"?<ErrorState message={preview.error} onRetry={preview.retry}/>:result&&<><div className="bookings-actions"><Badge variant={result.eligible?"success":"warning"}>{result.eligible?"Éligible":"Non éligible"}</Badge><span>{result.nbDossiers} dossier(s) associé(s)</span></div>{!result.eligible&&<Alert title="Aperçu indisponible" variant="warning">{result.raison}</Alert>}<Table caption="Aperçu des coûts à générer" rowKey="operation" rows={result.allocations} columns={[{key:"operation",header:"Opération"},{key:"montantMad",header:"Montant MAD",render:money},{key:"montantDevise",header:"Montant devise",render:money},{key:"nbDossiers",header:"Dossiers"}]} emptyState={<EmptyState title="Aucun nouveau coût à générer"/>}/><p>Total des opérations à générer : {money(result.totalMad)} MAD</p>{result.operationsIgnorees.length>0&&<Alert title="Opérations déjà générées">{result.operationsIgnorees.join(" · ")}</Alert>}</>)}
 <Button variant="primary" disabled={busy||blocked||resource.phase!=="ready"||resource.value.rates.avertissements.length>0||preview.phase!=="ready"||!result?.eligible||!result.allocations.length} onClick={()=>onGenerate(structuredClone(preview.value.request))}>Générer les coûts</Button>
 {generated?.keyVoyage===row.id&&<><Alert variant="success" title="Génération confirmée">Total retourné par le backend : {money(generated.totalMad)} MAD.</Alert><Table caption="Dernière génération confirmée" rowKey="operation" rows={generated.allocations} columns={[{key:"operation",header:"Opération"},{key:"montantMad",header:"Montant MAD",render:money}]}/></>}
 </div>;
}
LocalCosts.propTypes=VoyageCosts.propTypes;
ApiCosts.propTypes={row:PropTypes.object.isRequired,repository:PropTypes.object.isRequired,busy:PropTypes.bool,blocked:PropTypes.bool,generated:PropTypes.object,onGenerate:PropTypes.func.isRequired};
