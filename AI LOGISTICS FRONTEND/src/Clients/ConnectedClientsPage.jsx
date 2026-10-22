import { useMemo } from "react";
import { useAuth } from "../Auth/useAuth";
import { createClientsService } from "../services/clientsService";
import ClientsPage from "./ClientsPage";
export default function ConnectedClientsPage() {
  const { request } = useAuth();
  const repository = useMemo(() => createClientsService({ request }), [request]);
  return <ClientsPage repository={repository} />;
}
