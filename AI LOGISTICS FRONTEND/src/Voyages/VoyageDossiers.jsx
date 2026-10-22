import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { Badge, Button, EmptyState, ErrorState, LoadingState, Select, Table } from "../design-system";
import BookingReferencePicker from "../Bookings/BookingReferencePicker";
import {useBookingResource} from "../Bookings/useBookingResource";
import { billingStatuses } from "../Shipments/shipmentRules";
export default function VoyageDossiers(props){return props.repository?<ApiDossiers {...props}/>:<LocalDossiers {...props}/>;}
function LocalDossiers({ row, dossiers, busy, error, onExecute }) {
  const [selected, setSelected] = useState("");
  const attached = dossiers.filter((item) => item.keyVoyageInter === row.id), available = dossiers.filter((item) => !item.keyVoyageInter);
  return <div className="bookings-form"><Table caption="Dossiers associés au voyage" rows={attached} rowKey="keyDossier" className="bookings-detail-table" columns={[{ key: "numDossier", header: "Dossier" }, { key: "intituleClient", header: "Client" }, { key: "nbColis", header: "Colis" }, { key: "poidsTaxable", header: "Poids taxable (kg)" }, { key: "statutFacturation", header: "Facturation", render: (value) => <Badge variant={billingStatuses[value]?.variant}>{billingStatuses[value]?.label}</Badge> }, { key: "actions", header: "Actions", render: (_, item) => <Button disabled={busy || Boolean(row.dateCloture)} onClick={() => onExecute("detach", { dossierId: item.keyDossier })} aria-label={`Détacher ${item.numDossier}`}>Détacher</Button> }]} emptyState={<EmptyState title="Aucun dossier associé" />} />
    <Select label="Dossier à associer" disabled={busy || Boolean(row.dateCloture)} value={selected} onChange={(event) => setSelected(event.target.value)}><option value="">Sélectionnez un dossier libre</option>{available.map((item) => <option key={item.keyDossier} value={item.keyDossier}>{item.numDossier} · {item.intituleClient}</option>)}</Select>
    {!available.length && <p>Aucun dossier libre dans la démonstration.</p>}<Button variant="primary" disabled={busy || !selected || Boolean(row.dateCloture)} onClick={async () => { if (await onExecute("assign", { dossierId: selected })) setSelected(""); }}>Associer le dossier</Button>{error && <ErrorState message={error} />}
  </div>;
}
VoyageDossiers.propTypes = { row: PropTypes.object.isRequired, dossiers: PropTypes.array.isRequired, busy: PropTypes.bool,blocked:PropTypes.bool,repository:PropTypes.object,error: PropTypes.string, onExecute: PropTypes.func.isRequired };

function ApiDossiers({row,repository,busy,blocked,error,onExecute}){
 const [selected,setSelected]=useState("");const loader=useCallback(signal=>repository.dossiers(row.id,{signal}),[repository,row.id]);const result=useBookingResource(loader);
 return <div className="bookings-form">{result.phase==="loading"?<LoadingState label="Chargement des dossiers associés…"/>:result.phase==="error"?<ErrorState message={result.error} onRetry={result.retry}/>:<Table caption="Dossiers associés au voyage" rows={result.value} rowKey="keyDossier" className="bookings-detail-table" columns={[{key:"numDossier",header:"Dossier"},{key:"intituleClient",header:"Client"},{key:"nbColis",header:"Colis"},{key:"poidsTaxable",header:"Poids taxable (kg)"},{key:"statutFacturation",header:"Facturation",render:value=><Badge variant={billingStatuses[value]?.variant}>{billingStatuses[value]?.label}</Badge>},{key:"actions",header:"Actions",render:(_,item)=><Button disabled={busy||blocked||Boolean(row.dateCloture)} onClick={()=>onExecute("detach",{dossierId:item.keyDossier})} aria-label={`Détacher ${item.numDossier}`}>Détacher</Button>}]} emptyState={<EmptyState title="Aucun dossier associé"/>}/>}
 <BookingReferencePicker repository={repository} kind="dossiers" label="Dossier à associer" value={selected} onChange={setSelected} disabled={busy||blocked||Boolean(row.dateCloture)}/><p>Le voyage actuel est indiqué pour les dossiers déjà associés. Une nouvelle association peut les réaffecter selon les règles du backend.</p><Button variant="primary" disabled={busy||blocked||!selected||Boolean(row.dateCloture)} onClick={async()=>{if(await onExecute("assign",{dossierId:selected}))setSelected("");}}>Associer le dossier</Button>{error&&<ErrorState message={error}/>}</div>;
}
LocalDossiers.propTypes=VoyageDossiers.propTypes;
ApiDossiers.propTypes={row:PropTypes.object.isRequired,repository:PropTypes.object.isRequired,busy:PropTypes.bool,blocked:PropTypes.bool,error:PropTypes.string,onExecute:PropTypes.func.isRequired};
