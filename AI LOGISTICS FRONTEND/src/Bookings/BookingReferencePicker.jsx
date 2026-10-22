import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { Button, EmptyState, ErrorState, Input, LoadingState, Pagination, Select } from "../design-system";
import { useBookingResource } from "./useBookingResource";
export default function BookingReferencePicker({repository,kind,value,onChange,label:customLabel,placeholder,searchLabel,compact=false,disabled=false}) {
 const [search,setSearch]=useState(""),[page,setPage]=useState(1);
 const loader=useCallback(signal=>repository.references(kind,{signal,search,page}),[repository,kind,search,page]);
 const result=useBookingResource(loader), field=kind==="citernes"?"keyCiterne":kind==="voyages"?"keyVoyage":kind==="clients"?"keyClient":"keyDossier",label=customLabel||(kind==="citernes"?"Citerne":kind==="voyages"?"Voyage":kind==="clients"?"Client":"Dossier");
 return <div className={compact?"bookings-filter ds-field":"bookings-fields"}><Input disabled={disabled} label={searchLabel||`Rechercher un ${label.toLowerCase()}`} value={search} onChange={event=>{setSearch(event.target.value);setPage(1);}} />
 {result.phase==="loading"?<LoadingState label="Chargement des références…" />:result.phase==="error"?<ErrorState message={result.error} onRetry={result.retry} />:<>
 <Select disabled={disabled} label={label} value={value||""} onChange={event=>onChange(event.target.value,result.value.items.find(row=>row[field]===event.target.value)?.label || "")}><option value="">{placeholder || `Sélectionner ${label.toLowerCase()}`}</option>
 {value&&!result.value.items.some(row=>row[field]===value)&&<option value={value}>{value}</option>}
 {result.value.items.map(row=><option key={row[field]} value={row[field]}>{row.label}</option>)}</Select>
 {!result.value.items.length&&<EmptyState title="Aucune référence disponible" />}
 {result.value.totalCount>20&&<Pagination disabled={disabled} page={page} totalPages={Math.ceil(result.value.totalCount/20)} onPageChange={setPage} />}</>}
 {value&&<Button disabled={disabled} onClick={()=>onChange("","")}>Effacer la sélection</Button>}
 </div>;
}
BookingReferencePicker.propTypes={repository:PropTypes.object.isRequired,kind:PropTypes.oneOf(["voyages","dossiers","clients","citernes"]).isRequired,value:PropTypes.string,label:PropTypes.string,placeholder:PropTypes.string,searchLabel:PropTypes.string,compact:PropTypes.bool,disabled:PropTypes.bool,onChange:PropTypes.func.isRequired};
