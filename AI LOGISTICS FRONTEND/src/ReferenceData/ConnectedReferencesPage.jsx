import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createReferencesService} from "../services/referencesService";
import ReferencesPage from "./ReferencesPage";
export default function ConnectedReferencesPage(){const {request}=useAuth();const repository=useMemo(()=>createReferencesService({request}),[request]);return <ReferencesPage repository={repository}/>;}
