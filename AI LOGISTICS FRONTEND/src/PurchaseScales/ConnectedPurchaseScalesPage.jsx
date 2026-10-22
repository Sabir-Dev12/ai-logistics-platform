import { useMemo } from "react";
import { useAuth } from "../Auth/useAuth";
import { createScalesService } from "../services/scalesService";
import PurchaseScalesPage from "./PurchaseScalesPage";
export default function ConnectedPurchaseScalesPage() {
  const { request } = useAuth();
  const repository = useMemo(() => createScalesService({ request }), [request]);
  return <PurchaseScalesPage repository={repository} />;
}
