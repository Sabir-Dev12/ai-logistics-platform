import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createMcpToolsService} from "../services/mcpToolsService";
import McpToolsPage from "./McpToolsPage";
export default function ConnectedMcpToolsPage(){
 const {request}=useAuth();
 const repository=useMemo(()=>createMcpToolsService({request}),[request]);
 return <McpToolsPage repository={repository}/>;
}
