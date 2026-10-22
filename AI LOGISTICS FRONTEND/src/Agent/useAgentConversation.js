import {useEffect,useMemo,useRef,useState} from "react";
import {useChat} from "../Chat/useChat";
import {useChatSignalR} from "../hooks/useChatSignalR";
const empty=[];
export function useAgentConversation({transport,signalROptions}={}){
 const connection=useChatSignalR({...signalROptions,enabled:!transport});
 const ready=Boolean(transport)||connection.ready;
 const baseTransport=transport||connection.transport;
 const alive=useRef(false),generation=useRef(0),started=useRef(null);
 const [durationMs,setDurationMs]=useState(null),[validation,setValidation]=useState(null);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;};},[]);
 const measuredTransport=useMemo(()=>({async *stream(request){
  const id=++generation.current,start=performance.now();started.current=start;setDurationMs(null);
  try{yield* baseTransport.stream(request);}
  finally{if(alive.current&&generation.current===id){setDurationMs(Math.max(0,performance.now()-start));started.current=null;}}
 }}),[baseTransport]);
 const chat=useChat({transport:measuredTransport,initialMessages:empty,resetMessages:empty});
 const busy=chat.phase!=="idle";
 const user=chat.messages.findLast(message=>message.role==="user");
 const assistant=chat.messages.findLast(message=>message.role==="assistant");
 const status=busy?"running":assistant?.status==="cancelled"?"cancelled":assistant?.status==="error"?"error":assistant?.status==="complete"?"complete":"pending";
 const error=validation||chat.error;
 const steps=user?[
  {id:"question",label:"Question envoyée",status:"complete",durationMs:null},
  {id:"response",label:chat.phase==="waiting"?"En attente de la réponse serveur":"Flux de réponse",status,durationMs:null}
 ]:[];
 const stop=()=>{chat.stop();generation.current++;if(started.current!==null)setDurationMs(Math.max(0,performance.now()-started.current));started.current=null;};
 const reset=()=>{generation.current++;started.current=null;setDurationMs(null);setValidation(null);chat.reset();};
 return {run:{id:"live-agent",status,messages:chat.messages,steps,durationMs,sources:empty,tools:empty,error:error?{code:"Réponse de l’agent",message:error}:null},
  phase:chat.phase,busy,canSend:ready,canRetry:!validation,connectionStatus:transport?"connected":connection.status,connectionError:transport?null:connection.error,
  send(value){if(!ready||busy)return false;if(typeof value!=="string"||!value.trim()||value.trim().length>8000){setValidation("Saisissez une question de 1 à 8 000 caractères.");return false;}setValidation(null);return chat.send(value);},
  stop,reset,retry:()=>{setValidation(null);if(ready&&user)chat.retry();else connection.reconnect();}
 };
}
