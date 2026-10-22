import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createShipmentsService} from "../services/shipmentsService";
import ShipmentsPage from "./ShipmentsPage";
export default function ConnectedShipmentsPage(){const {request}=useAuth();const repository=useMemo(()=>createShipmentsService({request}),[request]);return <ShipmentsPage repository={repository}/>;}
