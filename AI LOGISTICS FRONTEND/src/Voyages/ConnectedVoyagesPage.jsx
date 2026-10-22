import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createVoyagesService} from "../services/voyagesService";
import VoyagesPage from "./VoyagesPage";
export default function ConnectedVoyagesPage(){const {request}=useAuth();const repository=useMemo(()=>createVoyagesService({request}),[request]);return <VoyagesPage repository={repository}/>;}
