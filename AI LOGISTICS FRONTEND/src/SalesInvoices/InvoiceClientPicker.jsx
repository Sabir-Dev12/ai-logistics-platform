import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { Button, ErrorState, Input, LoadingState, Pagination, Select } from "../design-system";
import { useBookingResource } from "../Bookings/useBookingResource";
export default function InvoiceClientPicker({source,value,onChange,label="Client",disabled=false,selectedLabel,allowAll=true}){
 const [search,setSearch]=useState(""),[page,setPage]=useState(1);
 const loader=useCallback(signal=>source.clients({signal,search,page}),[source,search,page]);const resource=useBookingResource(loader);const rows=resource.value?.items||[];
 return <div className="bookings-filter"><Input label={`Rechercher — ${label}`} value={search} disabled={disabled} onChange={event=>{setSearch(event.target.value);setPage(1);}}/>
 <Select label={label} value={value} disabled={disabled||resource.phase!=="ready"} onChange={event=>onChange(event.target.value)}><option value="">{allowAll?"Tous les clients":"Sélectionnez un client"}</option>{value&&!rows.some(row=>row.id===value)&&<option value={value}>{selectedLabel||value}</option>}{rows.map(row=><option key={row.id} value={row.id}>{row.label}</option>)}</Select>
 {resource.phase==="loading"&&<LoadingState label="Chargement des clients…"/>}{resource.phase==="error"&&<ErrorState message={resource.error} onRetry={resource.retry}/>}
 {resource.phase==="ready"&&!rows.length&&<p>Aucun client trouvé.</p>}
 {resource.value?.totalCount>20&&<Pagination page={page} totalPages={Math.ceil(resource.value.totalCount/20)} onPageChange={setPage} disabled={disabled}/>}
 {search&&<Button variant="ghost" disabled={disabled} onClick={()=>{setSearch("");setPage(1);}}>Effacer la recherche client</Button>}</div>;
}
InvoiceClientPicker.propTypes={source:PropTypes.object.isRequired,value:PropTypes.string.isRequired,onChange:PropTypes.func.isRequired,label:PropTypes.string,disabled:PropTypes.bool,selectedLabel:PropTypes.string,allowAll:PropTypes.bool};
