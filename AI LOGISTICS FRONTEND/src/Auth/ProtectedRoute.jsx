import { useEffect } from "react";
import PropTypes from "prop-types";
import { LoadingState } from "../design-system";
import { useAuth } from "./useAuth";
export default function ProtectedRoute({ children, onRedirect }) {
  const { status } = useAuth();
  useEffect(() => { if (status !== "authenticated" && status !== "loading") onRedirect(); }, [status, onRedirect]);
  return status === "authenticated" ? children : <LoadingState label="Vérification de la session…" />;
}
ProtectedRoute.propTypes = { children: PropTypes.node, onRedirect: PropTypes.func.isRequired };
