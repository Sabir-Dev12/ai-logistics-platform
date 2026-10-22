import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createPaymentsService} from "../services/paymentsService";
import PaymentsPage from "./PaymentsPage";
export default function ConnectedPaymentsPage(){const {request}=useAuth();const repository=useMemo(()=>createPaymentsService({request}),[request]);return <PaymentsPage repository={repository}/>;}
