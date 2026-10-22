import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createTrackingService} from "../services/trackingService";
import TrackingPage from "./TrackingPage";
export default function ConnectedTrackingPage(){const {request}=useAuth();const repository=useMemo(()=>createTrackingService({request}),[request]);return <TrackingPage repository={repository}/>;}
