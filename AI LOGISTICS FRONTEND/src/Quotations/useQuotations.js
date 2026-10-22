import { useMemo } from "react";
import { createMockQuotationRepository } from "./mockQuotationRepository";
import { useBusinessResource } from "../hooks/useBusinessResource";
export function useQuotations(repository, filters = {}) {
  const source = useMemo(() => repository || createMockQuotationRepository(), [repository]);
  return useBusinessResource(source, "quotations", filters);
}
