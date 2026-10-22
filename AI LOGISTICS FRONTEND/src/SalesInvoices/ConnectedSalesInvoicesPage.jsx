import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createInvoicesService} from "../services/invoicesService";
import SalesInvoicesPage from "./SalesInvoicesPage";
export default function ConnectedSalesInvoicesPage(){const {request}=useAuth();const repository=useMemo(()=>createInvoicesService({request}),[request]);return <SalesInvoicesPage repository={repository}/>;}
