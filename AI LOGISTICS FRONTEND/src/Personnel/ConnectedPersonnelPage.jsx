import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createPersonnelService} from "../services/personnelService";
import PersonnelPage from "./PersonnelPage";
export default function ConnectedPersonnelPage(){const {request}=useAuth();const repository=useMemo(()=>createPersonnelService({request}),[request]);return <PersonnelPage repository={repository}/>;}
