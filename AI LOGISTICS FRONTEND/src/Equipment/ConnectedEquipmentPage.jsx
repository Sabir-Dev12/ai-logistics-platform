import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createEquipmentService} from "../services/equipmentService";
import EquipmentPage from "./EquipmentPage";
export default function ConnectedEquipmentPage(){const {request}=useAuth();const repository=useMemo(()=>createEquipmentService({request}),[request]);return <EquipmentPage repository={repository}/>;}
