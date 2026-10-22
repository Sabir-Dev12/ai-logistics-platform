import {vi} from "vitest";
import {HubConnectionState} from "@microsoft/signalr";
import {createSignalRService} from "../../src/services/signalRService";
export function agentHubFixture({token="test-only",failure=null}={}){
 const events={},observers=[],disposed=vi.fn();
 const hub={state:HubConnectionState.Disconnected,
  start:vi.fn(async()=>{if(failure)throw failure;hub.state=HubConnectionState.Connected;}),
  stop:vi.fn(async()=>{hub.state=HubConnectionState.Disconnected;events.close?.();}),
  onreconnecting:callback=>{events.reconnecting=callback;},onreconnected:callback=>{events.reconnected=callback;},onclose:callback=>{events.close=callback;},
  stream:vi.fn(()=>({subscribe(observer){observers.push(observer);return {dispose:disposed};}}))
 };
 const serviceFactory=options=>createSignalRService({...options,connectionFactory:()=>hub,initialRetryDelays:[0]});
 return {hub,events,observers,disposed,options:{serviceFactory,accessTokenFactory:()=>token}};
}
