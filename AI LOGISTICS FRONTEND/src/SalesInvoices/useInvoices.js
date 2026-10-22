import { useCallback, useEffect, useRef, useState } from "react";
import { InvoicesApiError } from "../services/invoicesService";
import { InvoiceError } from "./invoiceRules";
import {useMemo} from "react";
import {createMockInvoiceRepository} from "./mockInvoiceRepository";
export function useInvoices(repository, filters = {}) {
 const source=useMemo(()=>repository||createMockInvoiceRepository(),[repository]);
 const serverPaging=Boolean(source?.serverPaging), filterKey=JSON.stringify(serverPaging?filters:{});
 const [state,setState]=useState({invoices:[],clients:[],phase:"loading",totalCount:0,key:null});
 const [detail,setDetail]=useState({phase:"idle",row:null,id:null,error:null});
 const [busy,setBusy]=useState(false), [error,setError]=useState(null), [uncertain,setUncertain]=useState(false);
 
 const mounted=useRef(false),load=useRef(null),detailLoad=useRef(null),mutation=useRef(null);
 const reload=useCallback(async()=>{ if(mutation.current)return; load.current?.abort();const controller=new AbortController();load.current=controller;
  setState(value=>({...value,phase:"loading",key:filterKey}));
  try{const result=await source.list({...JSON.parse(filterKey),signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setState({...result,phase:"ready",key:filterKey});}
  catch(failure){if(mounted.current&&!controller.signal.aborted)setState(value=>({...value,phase:"error",listError:failure instanceof InvoicesApiError||failure instanceof InvoiceError?failure.message:undefined,key:filterKey}));}
 },[source,filterKey]);
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;load.current?.abort();detailLoad.current?.abort();mutation.current?.abort();};},[source]);
 useEffect(()=>{void reload();return()=>load.current?.abort();},[reload]);
 const openDetail=useCallback(async(id)=>{if(!serverPaging)return;detailLoad.current?.abort();const controller=new AbortController();detailLoad.current=controller;setDetail({phase:"loading",row:null,id,error:null});
 try{const row=await source.get(id,{signal:controller.signal});if(mounted.current&&!controller.signal.aborted)setDetail({phase:"ready",row,id,error:null});}
 catch(failure){if(mounted.current&&!controller.signal.aborted)setDetail({phase:"error",row:null,id,error:failure instanceof InvoicesApiError?failure.message:"Impossible de charger la facture. Réessayez."});}
 },[source,serverPaging]);
 const execute=async(method,...args)=>{if(mutation.current||uncertain||state.phase!=="ready")return false;
 const controller=new AbortController();mutation.current=controller;setBusy(true);setError(null);let saved=false;
 try{const result=await source[method](...args,{signal:controller.signal});if(!mounted.current||controller.signal.aborted)return false;
 if(serverPaging){saved=true;}else setState({...result,phase:"ready",key:filterKey});return result;
 }catch(failure){if(mounted.current&&!controller.signal.aborted){setError(failure instanceof InvoiceError || failure instanceof InvoicesApiError?failure.message:"L’opération n’a pas pu aboutir. Réessayez.");setUncertain(Boolean(failure.uncertain));}return false;}
 finally{if(mutation.current===controller){mutation.current=null;if(mounted.current){setBusy(false);if(saved)void reload();}}}
 };
 return {...state,...(state.key!==filterKey?{phase:"loading",invoices:[],clients:[]}:{}),serverPaging,repository:source,detail,openDetail,busy,error,uncertain,execute,reload,
 clearError:()=>{setError(null);setUncertain(false);}};
}
