import { useCallback, useEffect, useRef, useState } from "react";
import { FuelApiError } from "../services/fuelService";
import { FuelError } from "./mockFuelRepository";
import {useMemo} from "react";
import {createMockFuelRepository} from "./mockFuelRepository";
export function useFuel(repository, filters = {}) {
 const source=useMemo(()=>repository||createMockFuelRepository(),[repository]);
 const serverPaging=Boolean(source?.serverPaging), filterKey=JSON.stringify(serverPaging?filters:{});
 const [state,setState]=useState({tanks:[],receptions:[],supplies:[],vouchers:[],issues:[],movements:[],gauges:[],ratios:[],equipment:[],drivers:[],suppliers:[],phase:"loading",totalCount:0,key:null});
 const [busy,setBusy]=useState(false), [error,setError]=useState(null), [uncertain,setUncertain]=useState(false);
 
 const mounted=useRef(false),load=useRef(null),mutation=useRef(null);
 const reload=useCallback(async()=>{ if(mutation.current)return; load.current?.abort();const controller=new AbortController();load.current=controller;
  setState(value=>({...value,phase:"loading",key:filterKey}));
  try{const result=await source.list({...JSON.parse(filterKey),signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setState({...result,phase:"ready",key:filterKey});}
  catch(failure){if(mounted.current&&!controller.signal.aborted)setState(value=>({...value,phase:"error",listError:failure instanceof FuelApiError||failure instanceof FuelError?failure.message:undefined,key:filterKey}));}
 },[source,filterKey]);
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;load.current?.abort();mutation.current?.abort();};},[source]);
 useEffect(()=>{void reload();return()=>load.current?.abort();},[reload]);
 const execute=async(method,...args)=>{if(mutation.current||uncertain||state.key!==filterKey||state.phase!=="ready")return false;
 const controller=new AbortController();mutation.current=controller;setBusy(true);setError(null);let saved=false;
 try{const result=await source[method](...args,{signal:controller.signal});if(!mounted.current||controller.signal.aborted)return false;
 if(serverPaging){saved=true;}else setState({...result,phase:"ready",key:filterKey});return result;
 }catch(failure){if(mounted.current&&!controller.signal.aborted){setError(failure instanceof FuelError || failure instanceof FuelApiError?failure.message:"L’opération n’a pas pu aboutir. Réessayez.");setUncertain(Boolean(failure.uncertain));}return false;}
 finally{if(mutation.current===controller){mutation.current=null;if(mounted.current){setBusy(false);if(saved)void reload();}}}
 };
 return {...state,...(state.key!==filterKey?{phase:"loading",information:null,tanks:[],receptions:[],supplies:[],vouchers:[],issues:[],movements:[],gauges:[],ratios:[],equipment:[],drivers:[],suppliers:[]}:{}),serverPaging,repository:source,busy,error,uncertain,execute,reload:()=>{setUncertain(false);setError(null);void reload();},
 clearError:()=>{if(!uncertain)setError(null);}};
}
