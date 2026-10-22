import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Button, EmptyState, ErrorState, Input, LoadingState, Select, Table } from "../design-system";
import BookingReferencePicker from "../Bookings/BookingReferencePicker";
import { useBookingResource } from "../Bookings/useBookingResource";
import { splitByTaxableWeight } from "./shipmentRules";
const format = (value) => new Intl.NumberFormat("fr-FR", { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(value);
export default function ShipmentVoyage(props){return props.repository?<ApiVoyage {...props}/>:<LocalVoyage {...props}/>;}
function LocalVoyage({ shipments, voyages, onOpen }) {
  const [voyage, setVoyage] = useState(""), [mad, setMad] = useState("1000"), [currency, setCurrency] = useState("100");
  const rows = shipments.filter((row) => row.keyVoyageInter === voyage);
  const valid = [mad, currency].every((value) => value !== "" && Number.isFinite(Number(value)) && Number(value) >= 0);
  const allocation = valid ? splitByTaxableWeight(rows, mad, currency) : [];
  return <div className="bookings-form"><Select label="Voyage à consulter" value={voyage} onChange={(event) => setVoyage(event.target.value)}><option value="">Sélectionnez un voyage</option>{voyages.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</Select>
    {!voyage ? <EmptyState title="Sélectionnez un voyage" description="Consultez ses dossiers et simulez leur répartition." /> : <><Table caption="Dossiers du voyage" rowKey="keyDossier" rows={rows} columns={[{ key: "numDossier", header: "Dossier", render: (value, row) => <Button variant="ghost" onClick={() => onOpen(row.keyDossier)}>{value}</Button> }, { key: "intituleClient", header: "Client" }, { key: "poidsTaxable", header: "Poids taxable (kg)" }]} emptyState={<EmptyState title="Aucun dossier sur ce voyage" />} />
    {rows.length > 0 && <><div className="bookings-fields"><Input label="Montant à répartir (MAD)" type="number" min={0} step="0.001" value={mad} onChange={(event) => setMad(event.target.value)} error={!valid ? "Renseignez des montants positifs ou nuls." : undefined} /><Input label="Montant à répartir (devise)" type="number" min={0} step="0.001" value={currency} onChange={(event) => setCurrency(event.target.value)} /></div>
      <Alert title="Simulation de répartition">Au prorata des poids taxables, arrondi à trois décimales. Aucun montant n’est enregistré ; les écarts d’arrondi éventuels ne sont pas compensés.</Alert>
      {valid && <Table caption="Répartition simulée par poids taxable" rows={allocation} rowKey="keyDossier" columns={[{ key: "numDossier", header: "Dossier" }, { key: "part", header: "Part (%)", render: format }, { key: "montantMad", header: "Montant MAD", render: format }, { key: "montantDevise", header: "Montant devise", render: format }]} emptyState={<EmptyState title="Répartition indisponible" description="Le poids taxable total du voyage doit être supérieur à zéro." />} />}</>}
    </>}
  </div>;
}
ShipmentVoyage.propTypes = { shipments: PropTypes.array.isRequired, voyages: PropTypes.array.isRequired, repository:PropTypes.object,onOpen: PropTypes.func.isRequired };


function ApiVoyage({repository,onOpen}){
 const [voyage,setVoyage]=useState(""),[mad,setMad]=useState("1000"),[currency,setCurrency]=useState("100");
 const valid=[mad,currency].every(value=>value!==""&&Number.isFinite(Number(value))&&Number(value)>=0);
 const loadRows=useCallback(signal=>voyage?repository.byVoyage(voyage,{signal}):Promise.resolve([]),[repository,voyage]);
 const rows=useBookingResource(loadRows);
 const loadAllocation=useCallback(signal=>voyage&&valid?repository.allocation(voyage,mad,currency,{signal}):Promise.resolve([]),[repository,voyage,valid,mad,currency]);
 const allocation=useBookingResource(loadAllocation);
 return <div className="bookings-form"><BookingReferencePicker repository={repository} kind="voyages" label="Voyage à consulter" value={voyage} onChange={setVoyage}/>
 {!voyage?<EmptyState title="Sélectionnez un voyage" description="Consultez ses dossiers et leur répartition."/>:rows.phase==="loading"?<LoadingState label="Chargement des dossiers du voyage…"/>:rows.phase==="error"?<ErrorState message={rows.error} onRetry={rows.retry}/>:<Table caption="Dossiers du voyage" rowKey="keyDossier" rows={rows.value} columns={[{key:"numDossier",header:"Dossier",render:(value,row)=><Button variant="ghost" onClick={()=>onOpen(row.keyDossier)}>{value}</Button>},{key:"intituleClient",header:"Client"},{key:"poidsTaxable",header:"Poids taxable (kg)"}]} emptyState={<EmptyState title="Aucun dossier sur ce voyage"/>}/>}
 {voyage&&<><div className="bookings-fields"><Input label="Montant à répartir (MAD)" type="number" min={0} step="0.001" value={mad} onChange={event=>setMad(event.target.value)} error={!valid?"Renseignez des montants positifs ou nuls.":undefined}/><Input label="Montant à répartir (devise)" type="number" min={0} step="0.001" value={currency} onChange={event=>setCurrency(event.target.value)}/></div><Alert title="Simulation de répartition">Calculée par le backend au prorata des poids taxables. Aucun montant n’est enregistré.</Alert>
 {valid&&(allocation.phase==="loading"?<LoadingState label="Calcul de la répartition…"/>:allocation.phase==="error"?<ErrorState message={allocation.error} onRetry={allocation.retry}/>:<Table caption="Répartition simulée par poids taxable" rows={allocation.value} rowKey="keyDossier" columns={[{key:"keyDossier",header:"Dossier"},{key:"montantMad",header:"Montant MAD",render:format},{key:"montantDevise",header:"Montant devise",render:format}]} emptyState={<EmptyState title="Répartition indisponible"/>}/>)}</>}
 </div>;
}
LocalVoyage.propTypes=ShipmentVoyage.propTypes;
ApiVoyage.propTypes={repository:PropTypes.object.isRequired,onOpen:PropTypes.func.isRequired};
