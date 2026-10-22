import {useMemo} from "react";
import {useAuth} from "../Auth/useAuth";
import {createKnowledgeBaseService} from "../services/knowledgeBaseService";
import KnowledgeBasePage from "./KnowledgeBasePage";
export default function ConnectedKnowledgeBasePage(){const {request}=useAuth();const repository=useMemo(()=>createKnowledgeBaseService({request}),[request]);return <KnowledgeBasePage repository={repository}/>;}
