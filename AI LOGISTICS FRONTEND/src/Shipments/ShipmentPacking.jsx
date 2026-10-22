import { useId, useRef, useState } from "react";
import PropTypes from "prop-types";
import { Button, Card, EmptyState, ErrorState, Input } from "../design-system";
import { validatePacking } from "./shipmentRules";
export default function ShipmentPacking(props){return props.remote?<ApiPacking {...props}/>:<LocalPacking {...props}/>;}
function LocalPacking({ row, busy, error, onSubmit, onCancel }) {
  const [lines, setLines] = useState(() => structuredClone(row.colisages));
  const [validation, setValidation] = useState(""); const sequence = useRef(0); const prefix = useId();
  const fields = [["typeColis", "Type de colis"], ["nbColis", "Nombre de colis", "number"], ["natureMarchandise", "Nature de la marchandise"], ["poidsBrut", "Poids brut (kg)", "number"], ["volume", "Volume (m³)", "number"], ["marque", "Marque"], ["codeNgp", "Code NGP"]];
  const submit = (event) => { event.preventDefault(); if (busy) return; try { validatePacking(lines); } catch (failure) { setValidation(failure.message); return; } void onSubmit(lines); };
  return <form className="bookings-form" onSubmit={submit} noValidate>
    {!lines.length && <EmptyState title="Aucun colisage" description="Ajoutez une ligne de colisage." />}
    {lines.map((line, index) => <Card key={line.id} className="bookings-line"><fieldset disabled={busy}><legend>Colisage {index + 1}</legend><div className="bookings-fields">{fields.map(([key, label, type = "text"]) => <Input key={key} label={`${label} — ligne ${index + 1}`} type={type} min={type === "number" ? 0 : undefined} step={type === "number" ? "any" : undefined} value={line[key] ?? ""} onChange={(event) => { setLines((current) => current.map((item) => item.id === line.id ? { ...item, [key]: event.target.value } : item)); setValidation(""); }} />)}</div>
      <Button onClick={() => { setLines((current) => current.filter((item) => item.id !== line.id)); setValidation(""); }}>Retirer le colisage {index + 1}</Button></fieldset></Card>)}
    <Button disabled={busy} onClick={() => setLines((current) => [...current, { id: `draft-${prefix}-${++sequence.current}`, typeColis: "", nbColis: 1 }])}>Ajouter un colisage</Button>
    <p>Les lignes détaillées et les totaux d’en-tête sont indépendants, comme dans le backend.</p>
    {(validation || error) && <ErrorState message={validation || error} />}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" loading={busy}>Enregistrer les colisages</Button></div>
  </form>;
}
ShipmentPacking.propTypes = { row: PropTypes.object.isRequired, busy: PropTypes.bool,remote:PropTypes.bool,blocked:PropTypes.bool,onAdd:PropTypes.func,onRemove:PropTypes.func,error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };



function ApiPacking({row,busy,blocked,error,onAdd,onRemove,onCancel}){
 const [draft,setDraft]=useState({typeColis:"",nbColis:1}),[pending,setPending]=useState(null),[validation,setValidation]=useState("");
 const fields=[["typeColis","Type de colis"],["nbColis","Nombre de colis","number"],["natureMarchandise","Nature de la marchandise"],["poidsBrut","Poids brut (kg)","number"],["volume","Volume (m³)","number"],["marque","Marque"],["codeNgp","Code NGP"]];
 const add=async event=>{event.preventDefault();if(busy||blocked)return;try{validatePacking([draft]);}catch(failure){setValidation(failure.message);return;}if(await onAdd(draft)){setDraft({typeColis:"",nbColis:1});setValidation("");}};
 return <div className="bookings-form">{!row.colisages.length&&<EmptyState title="Aucun colisage" description="Ajoutez une ligne de colisage."/>}
 {row.colisages.map((line,index)=><Card key={line.id} className="bookings-line"><h3>Colisage {index+1}</h3><div className="bookings-fields">{fields.map(([key,label])=><Input key={key} label={`${label} — ligne ${index+1}`} readOnly value={line[key]??""}/>)}</div><Button disabled={busy||blocked} onClick={()=>setPending(line.id)}>Retirer le colisage {index+1}</Button>{pending===line.id&&<div className="bookings-actions"><p>Confirmer la suppression de ce colisage ?</p><Button disabled={busy||blocked} onClick={async()=>{if(await onRemove(line.id))setPending(null);}}>Confirmer la suppression</Button><Button disabled={busy} onClick={()=>setPending(null)}>Conserver le colisage</Button></div>}</Card>)}
 <form className="bookings-form" onSubmit={add} noValidate><fieldset disabled={busy||blocked}><legend>Nouveau colisage</legend><div className="bookings-fields">{fields.map(([key,label,type="text"])=><Input key={key} label={label} type={type} min={type==="number"?0:undefined} step={type==="number"?"any":undefined} value={draft[key]??""} onChange={event=>{setDraft(current=>({...current,[key]:event.target.value}));setValidation("");}}/>)}</div></fieldset>
 {(validation||error)&&<ErrorState message={validation||error}/>}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Fermer les colisages</Button><Button type="submit" variant="primary" disabled={blocked} loading={busy}>Ajouter le colisage</Button></div></form>
 <p>Les lignes détaillées et les totaux d’en-tête sont indépendants. Les lignes existantes sont consultables et supprimables ; l’API ne propose pas leur modification.</p>
 </div>;
}
LocalPacking.propTypes=ShipmentPacking.propTypes;
ApiPacking.propTypes={row:PropTypes.object.isRequired,busy:PropTypes.bool,blocked:PropTypes.bool,error:PropTypes.string,onAdd:PropTypes.func.isRequired,onRemove:PropTypes.func.isRequired,onCancel:PropTypes.func.isRequired};
