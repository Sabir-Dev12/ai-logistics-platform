import { useMemo } from "react";
import { useAuth } from "../Auth/useAuth";
import { createQuotationsService } from "../services/quotationsService";
import QuotationsPage from "./QuotationsPage";
export default function ConnectedQuotationsPage() {
  const { request } = useAuth();
  const repository = useMemo(() => createQuotationsService({ request }), [request]);
  return <QuotationsPage repository={repository} />;
}
