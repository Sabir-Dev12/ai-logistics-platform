import { useCallback, useEffect, useRef, useState } from "react";
import { ReferencesApiError } from "../services/referencesService";
import { ReferenceError } from "./mockReferenceRepository";
import {useMemo} from "react";
import {createMockReferenceRepository} from "./mockReferenceRepository";
export function useReferences(repository, filters = {}) {
 const source=useMemo(()=>repository||createMockReferenceRepository(),[repository]);
 const serverPaging=Boolean(source?.remote), filterKey=JSON.stringify(serverPaging?filters:{});
 const [state,setState]=useState({entries:{},company:null,canCreate:false,canDelete:false,canCheck:false,phase:"loading",totalCount:0,key:null});
 const [busy,setBusy]=useState(false), [error,setError]=useState(null), [uncertain,setUncertain]=useState(false);
 
 const mounted=useRef(false),load=useRef(null),mutation=useRef(null);
 const reload=useCallback(async()=>{ if(mutation.current)return; load.current?.abort();const controller=new AbortController();load.current=controller;
  setState(value=>({...value,phase:"loading",key:filterKey}));
  try{const result=await source.list({...JSON.parse(filterKey),signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setState({...result,phase:"ready",key:filterKey});}
  catch(failure){if(mounted.current&&!controller.signal.aborted)setState(value=>({...value,phase:"error",listError:failure instanceof ReferencesApiError||failure instanceof ReferenceError?failure.message:undefined,key:filterKey}));}
 },[source,filterKey]);
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;load.current?.abort();mutation.current?.abort();};},[source]);
 useEffect(()=>{void reload();return()=>load.current?.abort();},[reload]);
 const execute=async(method,...args)=>{if(mutation.current||(uncertain&&method!=="check")||state.key!==filterKey||state.phase!=="ready")return false;
 const controller=new AbortController();mutation.current=controller;setBusy(true);setError(null);let saved=false;
 try{const result=await source[method](...args,{signal:controller.signal});if(!mounted.current||controller.signal.aborted)return false;
 if(serverPaging){if(method!=="check"){saved=true;setState(value=>({...value,entries:{...value.entries,[args[0]]:result.row?[result.row,...(value.entries[args[0]]||[]).filter(row=>row.id!==result.row.id)]:(value.entries[args[0]]||[]).filter(row=>row.id!==result.removedId)}}));}}else if(method!=="check") setState({...result,phase:"ready",key:filterKey});return result;
 }catch(failure){if(mounted.current&&!controller.signal.aborted){setError(failure instanceof ReferenceError || failure instanceof ReferencesApiError?failure.message:"L’opération n’a pas pu aboutir. Réessayez.");setUncertain(value=>value||Boolean(failure.uncertain));}return false;}
 finally{if(mutation.current===controller){mutation.current=null;if(mounted.current){setBusy(false);if(saved)void reload();}}}
 };
 return {...state,...(state.key!==filterKey?{phase:"loading",information:null,entries:{},company:null,canCreate:false,canDelete:false,canCheck:false}:{}),remote:serverPaging,serverPaging,repository:source,busy,error,uncertain,execute,reload:()=>{setUncertain(false);setError(null);void reload();},
 clearError:()=>{if(!uncertain)setError(null);}};
}
