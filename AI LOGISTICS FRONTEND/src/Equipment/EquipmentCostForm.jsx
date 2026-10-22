import {useCallback} from "react";
import PropTypes from "prop-types";
import {useBookingResource} from "../Bookings/useBookingResource";
import {ErrorState,LoadingState} from "../design-system";
import MonthlyCostForm from "../Resources/MonthlyCostForm";
export default function EquipmentCostForm({remote,repository,...props}){
 const load=useCallback(signal=>remote?repository.currencies({signal}):Promise.resolve(null),[remote,repository]);
 const result=useBookingResource(load);
 if(remote&&result.phase!=="ready")return result.phase==="error"?<ErrorState message={result.error} onRetry={result.retry}/>:<LoadingState label="Chargement des devises…"/>;
 return <MonthlyCostForm {...props} subject="ce matériel" {...(remote?{currencies:result.value}:{})}/>;
}
EquipmentCostForm.propTypes={remote:PropTypes.bool,repository:PropTypes.object};
