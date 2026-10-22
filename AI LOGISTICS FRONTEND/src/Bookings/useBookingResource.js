import { useCallback, useEffect, useRef, useState } from "react";
export function useBookingResource(loader) {
 const [revision,setRevision]=useState(0), [state,setState]=useState({phase:"loading",value:null,error:null,loader:null});
 const sequence=useRef(0);
 useEffect(()=>{ const controller=new AbortController(),id=++sequence.current;
   setState({phase:"loading",value:null,error:null,loader});
   Promise.resolve().then(()=>loader(controller.signal)).then(value=>{if(!controller.signal.aborted && sequence.current===id)setState({phase:"ready",value,error:null,loader});})
   .catch(error=>{if(!controller.signal.aborted && sequence.current===id)setState({phase:"error",value:null,error:error.message,loader});});
   return ()=>controller.abort();
 },[loader,revision]);
 const retry=useCallback(()=>setRevision(value=>value+1),[]);
 return {...(state.loader===loader?state:{phase:"loading",value:null,error:null}),retry};
}
