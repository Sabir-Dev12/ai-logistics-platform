import {useCallback,useEffect,useRef,useState} from "react";
import {TrackingError} from "./trackingRules";
import {TrackingApiError} from "../services/trackingService";
export function useTracking(source,author,filters={}){
 const remote=Boolean(source?.remoteTracking),filterKey=JSON.stringify(remote?filters:{});
 const [state,setState]=useState({records:[],statuses:[],events:[],canRecord:false,phase:"loading",key:null});
 const [busy,setBusy]=useState(false),[error,setError]=useState(null),[uncertain,setUncertain]=useState(false);
 const mounted=useRef(false),load=useRef(null),mutation=useRef(null);
 const reload=useCallback(async()=>{if(mutation.current)return;load.current?.abort();const controller=new AbortController();load.current=controller;setState(current=>({...current,phase:"loading",key:filterKey}));try{const result=await source.list({...JSON.parse(filterKey),signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setState({...result,phase:"ready",key:filterKey});}catch(failure){if(mounted.current&&!controller.signal.aborted)setState(current=>({...current,phase:"error",loadError:failure instanceof TrackingApiError?failure.message:undefined,key:filterKey}));}},[source,filterKey]);
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;load.current?.abort();mutation.current?.abort();};},[source]);
 useEffect(()=>{void reload();return()=>load.current?.abort();},[reload]);
 const execute=async(method,...args)=>{if(mutation.current||uncertain||state.phase!=="ready")return false;const controller=new AbortController();mutation.current=controller;setBusy(true);setError(null);let saved=false;try{const result=await source[method](...args,{signal:controller.signal});if(!mounted.current||controller.signal.aborted)return false;if(remote)saved=true;else setState({...result,phase:"ready",key:filterKey});return result;}catch(failure){if(mounted.current&&!controller.signal.aborted){setError(failure instanceof TrackingError||failure instanceof TrackingApiError?failure.message:"L’opération n’a pas pu aboutir. Réessayez.");setUncertain(Boolean(failure.uncertain));}return false;}finally{if(mutation.current===controller){mutation.current=null;if(mounted.current){setBusy(false);if(saved)void reload();}}}};
 return {...state,...(state.key!==filterKey?{phase:"loading",events:[],latest:null,statuses:[],canRecord:false}:{}),remote,busy,error,uncertain,reload,execute,clearError:()=>{setError(null);setUncertain(false);}};
}
