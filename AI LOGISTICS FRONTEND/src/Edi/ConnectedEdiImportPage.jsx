import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createEdiService} from "../services/ediService";
import EdiImportPage from "./EdiImportPage";
export default function ConnectedEdiImportPage(){const {request}=useAuth();const repository=useMemo(()=>createEdiService({request}),[request]);return <EdiImportPage repository={repository}/>;}
