import PropTypes from "prop-types";

export const documentTypes = ["PDF", "DOCX", "TXT", "MD", "CSV"];
export const documentStatuses = { indexed: "Indexé", processing: "En cours", failed: "Erreur" };
export const documentShape = PropTypes.shape({
  id: PropTypes.string.isRequired, name: PropTypes.string.isRequired,
  type: PropTypes.oneOf(documentTypes).isRequired, size: PropTypes.number.isRequired,
  date: PropTypes.string.isRequired, status: PropTypes.oneOf(Object.keys(documentStatuses)).isRequired,
});
export const normalizeSearch = (value) => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("fr").trim();
export const fileType = (name) => name.split(".").at(-1).toUpperCase();
export function formatSize(bytes) {
  const units = ["o", "Ko", "Mo", "Go"];
  let size = bytes; let unit = 0;
  while (size >= 1024 && unit < units.length - 1) { size /= 1024; unit += 1; }
  return `${new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 1 }).format(size)} ${units[unit]}`;
}
export const formatDate = (value) => new Intl.DateTimeFormat("fr-FR", { timeZone: "Africa/Casablanca" }).format(new Date(value));

export const documentLimits={maxFiles:10,maxFileBytes:10*1024*1024,maxBatchBytes:49*1024*1024};
export function validateDocumentBatch(files){
 if(!Array.isArray(files)||files.length<1||files.length>documentLimits.maxFiles)return "Sélectionnez entre 1 et 10 fichiers.";
 if(files.some(file=>!file||typeof file.name!=="string"||file.name.length>200||/[\\/\0]/.test(file.name)||!documentTypes.includes(fileType(file.name))))return "Formats autorisés : PDF, DOCX, TXT, MD, CSV ; nom de fichier simple requis (200 caractères maximum).";
 if(files.some(file=>!Number.isSafeInteger(file.size)||file.size<1||file.size>documentLimits.maxFileBytes))return "Chaque fichier doit être non vide et inférieur ou égal à 10 Mo.";
 if(files.reduce((sum,file)=>sum+file.size,0)>documentLimits.maxBatchBytes)return "Le lot ne doit pas dépasser 49 Mo au total.";
 return null;
}
export const documentErrorLabel=code=>({no_extractable_text:"Aucun texte exploitable. Pour un PDF scanné, fournissez une version contenant du texte.",invalid_document:"Document illisible ou invalide.",document_too_large:"Le contenu dépasse les limites d’extraction.",indexing_cancelled:"L’indexation a été interrompue.",indexing_failed:"L’indexation a échoué ; vérifiez le service d’embeddings."}[code]||"L’indexation n’a pas abouti.");
