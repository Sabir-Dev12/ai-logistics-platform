import { useCallback, useEffect, useRef, useState } from "react";
import { BookingRuleError, bookingMessages } from "./bookingRules";
export function useBookings(source, filters = {}) {
 const serverPaging=Boolean(source?.serverPaging), filterKey=JSON.stringify(serverPaging?filters:{});
 const [state,setState]=useState({bookings:[],voyages:[],dossiers:[],phase:"loading",totalCount:0,key:null});
 const [detail,setDetail]=useState({phase:"idle",booking:null,id:null,error:null});
 const [busy,setBusy]=useState(false), [error,setError]=useState(null), [uncertain,setUncertain]=useState(false);
 const mounted=useRef(false),load=useRef(null),detailLoad=useRef(null),mutation=useRef(null);
 const reload=useCallback(async()=>{ if(mutation.current)return; load.current?.abort();const controller=new AbortController();load.current=controller;
  setState(value=>({...value,phase:"loading",key:filterKey}));
  try{const result=await source.list({...JSON.parse(filterKey),signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setState({...result,voyages:result.voyages||[],dossiers:result.dossiers||[],phase:"ready",key:filterKey});}
  catch(failure){if(mounted.current&&!controller.signal.aborted)setState(value=>({...value,phase:"error",listError:serverPaging?failure.message:undefined,key:filterKey}));}
 },[source,filterKey,serverPaging]);
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;load.current?.abort();detailLoad.current?.abort();mutation.current?.abort();};},[source]);
 useEffect(()=>{void reload();return()=>load.current?.abort();},[reload]);
 const openDetail=useCallback(async(id)=>{if(!serverPaging)return;detailLoad.current?.abort();const controller=new AbortController();detailLoad.current=controller;setDetail({phase:"loading",booking:null,id,error:null});
 try{const booking=await source.get(id,{signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setDetail({phase:"ready",booking,id,error:null});}
 catch(failure){if(mounted.current&&!controller.signal.aborted)setDetail({phase:"error",booking:null,id,error:failure.message});}
 },[source,serverPaging]);
 const execute=async(method,...args)=>{if(mutation.current||uncertain||state.phase!=="ready")return false;
 const controller=new AbortController();mutation.current=controller;setBusy(true);setError(null);let saved=false;
 try{const result=await source[method](...args,{signal:controller.signal});if(!mounted.current||controller.signal.aborted)return false;
 if(serverPaging){saved=true;}else setState({...result,phase:"ready",key:filterKey});return result;
 }catch(failure){if(mounted.current&&!controller.signal.aborted){setError(failure instanceof BookingRuleError?bookingMessages[failure.code]:failure.message||bookingMessages.unavailable);setUncertain(Boolean(failure.uncertain));}return false;}
 finally{if(mutation.current===controller){mutation.current=null;if(mounted.current){setBusy(false);if(saved)void reload();}}}
 };
 return {...state,...(state.key!==filterKey?{phase:"loading",bookings:[]}:{}),serverPaging,repository:source,detail,openDetail,busy,error,uncertain,execute,reload,
 clearError:()=>{setError(null);setUncertain(false);}};
}
