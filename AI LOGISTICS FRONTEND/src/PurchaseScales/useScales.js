import { useMemo } from "react";
import { createMockScaleRepository } from "./mockScaleRepository";
import { useBusinessResource } from "../hooks/useBusinessResource";
export function useScales(repository, filters = {}) {
  const source = useMemo(() => repository || createMockScaleRepository(), [repository]);
  return useBusinessResource(source, "scales", filters);
}
