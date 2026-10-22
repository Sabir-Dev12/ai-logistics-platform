import { useMemo } from "react";
import { useAuth } from "../Auth/useAuth";
import { createAccountService } from "../services/accountService";
import AccountPage from "./AccountPage";
export default function ConnectedAccountPage() {
  const auth = useAuth();
  const repository = useMemo(() => createAccountService({ request: auth.request, logout: auth.logout }), [auth.request, auth.logout]);
  return <AccountPage repository={repository} />;
}
