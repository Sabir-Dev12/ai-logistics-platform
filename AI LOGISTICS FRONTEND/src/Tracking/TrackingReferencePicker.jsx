import {useCallback,useEffect,useState} from "react";
import PropTypes from "prop-types";
import {EmptyState,ErrorState,Input,LoadingState,Pagination,Select} from "../design-system";
import {useBookingResource} from "../Bookings/useBookingResource";
import {trackingReferences} from "../services/trackingService";
export default function TrackingReferencePicker({repository,entity,value,label,onChange,disabled}){
 const [search,setSearch]=useState(""),[page,setPage]=useState(1);
 const loader=useCallback(signal=>repository.references(entity,{signal,search,page}),[repository,entity,search,page]);
 const result=useBookingResource(loader);
 useEffect(()=>{if(result.phase==="ready"&&!value&&result.value.items.length){const first=result.value.items[0];onChange(first.key,first.label);}},[result.phase,result.value,value,onChange]);
 return <>{trackingReferences[entity]?.search&&<Input fieldClassName="bookings-search" label="Rechercher une référence" value={search} disabled={disabled} onChange={event=>{setSearch(event.target.value);setPage(1);}}/>}
 {result.phase==="loading"?<LoadingState label="Chargement des références…"/>:result.phase==="error"?<ErrorState message={result.error} onRetry={result.retry}/>:<><Select fieldClassName="bookings-search" label="Référence à suivre" value={value} disabled={disabled} onChange={event=>{const item=result.value.items.find(row=>row.key===event.target.value);onChange(event.target.value,item?.label||event.target.value);}}><option value="">Sélectionnez une référence</option>{value&&!result.value.items.some(row=>row.key===value)&&<option value={value}>{label||value}</option>}{result.value.items.map(row=><option key={row.key} value={row.key}>{row.label}</option>)}</Select>{!result.value.items.length&&<EmptyState title="Aucune référence disponible"/>}{result.value.totalCount>20&&<Pagination page={page} totalPages={Math.ceil(result.value.totalCount/20)} onPageChange={setPage} disabled={disabled}/>}</>}
 </>;
}
TrackingReferencePicker.propTypes={repository:PropTypes.object.isRequired,entity:PropTypes.string.isRequired,value:PropTypes.string.isRequired,label:PropTypes.string,onChange:PropTypes.func.isRequired,disabled:PropTypes.bool};
