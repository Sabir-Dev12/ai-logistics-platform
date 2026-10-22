import {AuthenticationError} from "./authenticationService";
export class McpToolsApiError extends Error {
 constructor(code,message){super(message);this.name="McpToolsApiError";this.code=code;}
}
const invalid=()=>new McpToolsApiError("invalid_response","Le catalogue MCP reçu est invalide. Réessayez.");
const isObject=value=>value!==null&&typeof value==="object"&&!Array.isArray(value);
const categories={GetClient:"Clients",GetInvoice:"Facturation",GetShipment:"Expéditions",GetTracking:"Suivi",GetBooking:"Réservations"};
const maxResponse=2*1024*1024;
function envelope(message,id){
 if(!isObject(message)||message.jsonrpc!=="2.0"||message.id!==id||Object.hasOwn(message,"result")===Object.hasOwn(message,"error"))throw invalid();
 if(message.error)throw new McpToolsApiError("protocol_error","Le serveur MCP a refusé la demande de catalogue. Réessayez.");
 if(!isObject(message.result))throw invalid();return message.result;
}
async function result(response,id){
 const text=await response.text();if(!text||text.length>maxResponse)throw invalid();
 const contentType=response.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase();
 try{
  if(contentType==="application/json")return envelope(JSON.parse(text),id);
  if(contentType!=="text/event-stream")throw invalid();
  // Streamable HTTP may send comments, priming events and notifications before the response.
  for(const event of text.replace(/\r\n|\r/g,"\n").split("\n\n")){
   const data=event.split("\n").filter(line=>line.startsWith("data:")).map(line=>line.slice(5).replace(/^ /,"")).join("\n");
   if(!data.trim())continue;const message=JSON.parse(data);
   if(message?.id===id)return envelope(message,id);
  }
  throw invalid();
 }catch(error){if(error instanceof McpToolsApiError)throw error;throw invalid();}
}
function tool(raw){
 if(!isObject(raw)||typeof raw.name!=="string"||!raw.name.trim()||raw.description!=null&&typeof raw.description!=="string"||!isObject(raw.inputSchema)||raw.inputSchema.type!=="object")throw invalid();
 const schema=raw.inputSchema,properties=schema.properties??{},required=schema.required??[];
 if(!isObject(properties)||!Array.isArray(required)||required.some(name=>typeof name!=="string"||!Object.hasOwn(properties,name))||Object.values(properties).some(value=>!isObject(value)))throw invalid();
 const parameters=Object.entries(properties).map(([name,value])=>({name,type:Array.isArray(value.type)?value.type.join(" | "):typeof value.type==="string"?value.type:"Voir schéma",required:required.includes(name),description:typeof value.description==="string"?value.description:"Voir schéma d’entrée."}));
 return {name:raw.name,description:raw.description||"Aucune description fournie.",category:categories[raw.name]||"Autres",status:"published",parameters,inputSchema:schema,outputSchema:raw.outputSchema??null,annotations:raw.annotations??null,lastUsed:null,lastInput:null,result:null,error:null};
}
export function createMcpToolsService({request,timeoutMs=15000}){
 let sequence=0;
 return {remote:true,async list({signal}={}){
  const controller=new AbortController(),abort=()=>controller.abort();
  if(signal?.aborted)controller.abort();else signal?.addEventListener("abort",abort,{once:true});
  let timedOut=false;const timer=setTimeout(()=>{timedOut=true;controller.abort();},timeoutMs);
  async function post(message,version){
   if(controller.signal.aborted)throw new DOMException("Annulé","AbortError");
   const response=await request("/mcp",{method:"POST",signal:controller.signal,headers:{Accept:"application/json, text/event-stream","Content-Type":"application/json",...(version?{"MCP-Protocol-Version":version}:{})},body:JSON.stringify(message)});
   if(!response.ok){const code=({401:"unauthorized",403:"forbidden",429:"rate_limit",404:"missing"})[response.status]||"unavailable";
    throw new McpToolsApiError(code,({unauthorized:"Votre session n’est plus autorisée. Reconnectez-vous.",forbidden:"Accès MCP refusé. Vérifiez vos droits et les origines autorisées du backend.",rate_limit:"Trop de demandes. Patientez avant de réessayer.",missing:"Le serveur ne fournit pas l’endpoint MCP /mcp."})[code]||"Le service MCP est indisponible. Réessayez.");}
   return response;
  }
  try{
   const initId=++sequence;
   const initialization=await post({jsonrpc:"2.0",id:initId,method:"initialize",params:{protocolVersion:"2025-11-25",capabilities:{},clientInfo:{name:"ai-logistics-catalogue",version:"1.0.0"}}});
   if(initialization.status!==200)throw invalid();const initialized=await result(initialization,initId);
   if(initialized.protocolVersion!=="2025-11-25")throw new McpToolsApiError("unsupported_protocol","La version MCP du serveur n’est pas compatible avec ce catalogue.");
   if(!isObject(initialized.capabilities)||!isObject(initialized.capabilities.tools))throw new McpToolsApiError("unsupported_tools","Ce serveur MCP ne publie pas de catalogue de tools.");
   // The existing backend is stateless: never keep a session identifier or a token here.
   if(initialization.headers.get("Mcp-Session-Id"))throw new McpToolsApiError("unsupported_session","Ce catalogue attend le transport MCP sans session configuré dans le backend.");
   const notification=await post({jsonrpc:"2.0",method:"notifications/initialized"},initialized.protocolVersion);
   if(notification.status!==202)throw invalid();
   const tools=[],seenNames=new Set(),seenCursors=new Set();let cursor;
   do{
    const id=++sequence;const response=await post({jsonrpc:"2.0",id,method:"tools/list",params:cursor?{cursor}:{}},initialized.protocolVersion);
    if(response.status!==200)throw invalid();const page=await result(response,id);
    if(!Array.isArray(page.tools))throw invalid();
    for(const raw of page.tools){const row=tool(raw);if(seenNames.has(row.name)||tools.length>=500)throw invalid();seenNames.add(row.name);tools.push(row);}
    cursor=page.nextCursor;if(cursor!==undefined&&(typeof cursor!=="string"||!cursor||seenCursors.has(cursor)||seenCursors.size>=100))throw invalid();if(cursor)seenCursors.add(cursor);
   }while(cursor);
   return tools;
  }catch(error){
   if(signal?.aborted)throw new DOMException("Annulé","AbortError");
   if(timedOut)throw new McpToolsApiError("timeout","Le serveur MCP met trop de temps à répondre. Réessayez.");
   if(error instanceof AuthenticationError)throw new McpToolsApiError(error.code,error.message);
   if(error instanceof McpToolsApiError)throw error;
   throw new McpToolsApiError("network","Impossible de joindre le serveur MCP. Vérifiez votre connexion puis réessayez.");
  }finally{clearTimeout(timer);signal?.removeEventListener("abort",abort);}
 }};
}
