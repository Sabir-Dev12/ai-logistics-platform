import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createFuelService} from "../services/fuelService";
import FuelPage from "./FuelPage";
export default function ConnectedFuelPage(){const {request}=useAuth();const repository=useMemo(()=>createFuelService({request}),[request]);return <FuelPage repository={repository}/>;}
