import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createMockDocumentRepository } from "./mockDocumentRepository";
import { KnowledgeBaseApiError } from "../services/knowledgeBaseService";
export function useDocuments(repository,filters={}) {
 const source=useMemo(()=>repository||createMockDocumentRepository(),[repository]);
 const serverPaging=Boolean(source.serverPaging);
 const filterKey=serverPaging?JSON.stringify(filters):"{}";
 const [state,setState]=useState({documents:[],totalCount:0,phase:"loading",key:null,listError:null});
 const [busy,setBusy]=useState(false),[actionError,setActionError]=useState(null),[uncertain,setUncertain]=useState(false),[uploadOutcome,setUploadOutcome]=useState([]);
 const mounted=useRef(false),activeLoad=useRef(null),activeMutation=useRef(null),unlock=useRef(false),locked=useRef(false);
 const load=useCallback(async()=>{
  activeLoad.current?.abort();const controller=new AbortController();activeLoad.current=controller;
  setState(current=>({...current,phase:"loading",key:filterKey,listError:null}));
  try{
   const result=await source.list({...JSON.parse(filterKey),signal:controller.signal});
   if(mounted.current&&!controller.signal.aborted){setState({documents:serverPaging?result.documents:result,totalCount:serverPaging?result.totalCount:result.length,phase:"ready",key:filterKey,listError:null});if(unlock.current){locked.current=false;setUncertain(false);unlock.current=false;}}
  }catch(error){if(mounted.current&&!controller.signal.aborted)setState(current=>({...current,phase:"error",key:filterKey,listError:error instanceof KnowledgeBaseApiError?error.message:null}));}
 },[source,serverPaging,filterKey]);
 useEffect(()=>{mounted.current=true;void load();return()=>{mounted.current=false;activeLoad.current?.abort();activeMutation.current?.abort();};},[load]);
 const reload=()=>{if(activeMutation.current)return;unlock.current=true;void load();};
 const phase=state.key===filterKey?state.phase:"loading";
 async function mutate(operation,message){
  if(activeMutation.current||phase!=="ready"||locked.current)return false;
  const controller=new AbortController();activeMutation.current=controller;setBusy(true);setActionError(null);
  try{await operation(controller.signal);return mounted.current&&!controller.signal.aborted;}
  catch(error){if(mounted.current&&!controller.signal.aborted){setActionError(error instanceof KnowledgeBaseApiError?error.message:message);if(error.uncertain){locked.current=true;unlock.current=false;setUncertain(true);}}return false;}
  finally{if(activeMutation.current===controller){activeMutation.current=null;if(mounted.current)setBusy(false);}}
 }
 return {...state,phase,serverPaging,busy,uncertain,actionError,uploadOutcome,reload,clearActionError:()=>setActionError(null),clearUploadOutcome:()=>setUploadOutcome([]),
  upload:files=>mutate(async signal=>{const added=await source.upload(files,{signal});if(!mounted.current||signal.aborted)return;if(serverPaging){setUploadOutcome(added);void load();}else setState(current=>({...current,documents:[...added,...current.documents]}));},"Impossible d’ajouter les documents. Réessayez."),
  remove:id=>mutate(async signal=>{await source.remove(id,{signal});if(!mounted.current||signal.aborted)return;if(serverPaging)void load();else setState(current=>({...current,documents:current.documents.filter(row=>row.id!==id)}));},"Impossible de supprimer le document. Réessayez.")
 };
}
