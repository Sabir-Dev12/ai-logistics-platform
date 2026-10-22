import { useMemo } from "react";
import { createMockClientRepository } from "./mockClientRepository";
import { useBusinessResource } from "../hooks/useBusinessResource";
export function useClients(repository, filters = {}) {
  const source = useMemo(() => repository || createMockClientRepository(), [repository]);
  return useBusinessResource(source, "clients", filters);
}
