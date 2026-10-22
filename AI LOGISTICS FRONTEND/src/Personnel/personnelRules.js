export { currencies, money } from "../SalesInvoices/invoiceRules";
export class PersonnelError extends Error {}
export const guard = (condition, message) => { if (!condition) throw new PersonnelError(message); };
export const fullName = (employee) => [employee.lastName, employee.firstName].filter(Boolean).join(" ");
export function validateEmployee(value, employees, references, id) {
  guard(value.lastName?.trim(), "Le nom est obligatoire.");
  guard(!value.email || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.email.trim()), "Renseignez une adresse email valide.");
  guard(!value.employeeNumber?.trim() || !employees.some((employee) => employee.id !== id && employee.employeeNumber.toLowerCase() === value.employeeNumber.trim().toLowerCase()), "Ce matricule est déjà utilisé.");
  for (const [key, collection] of [["functionId", "functions"], ["serviceId", "services"], ["agencyId", "agencies"]]) guard(!value[key] || references[collection].some((row) => row.id === value[key]), "Sélectionnez une référence valide.");
  const employee = employees.find((row) => row.id === id);
  guard(!employee?.departureDate || !value.hireDate || value.hireDate <= employee.departureDate, "La date d’embauche ne peut pas être postérieure à la sortie.");
}
