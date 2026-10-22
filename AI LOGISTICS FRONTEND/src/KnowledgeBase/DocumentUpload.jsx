import { useEffect, useRef, useState } from "react";
import PropTypes from "prop-types";
import { Button, ErrorState, Input, Modal } from "../design-system";
import { documentTypes, fileType, formatSize, validateDocumentBatch } from "./documentTypes";

export default function DocumentUpload({ open, onClose, onUpload, busy, error, remote = false, blocked = false }) {
  const [files, setFiles] = useState([]);
  const [validation, setValidation] = useState(null);
  const input = useRef(null);
  useEffect(() => { if (open) { setFiles([]); setValidation(null); } }, [open]);
  const select = (event) => {
    const selected = Array.from(event.target.files || []);
    event.target.value = "";
    const invalid = selected.find((file) => !documentTypes.includes(fileType(file.name)) || file.size === 0);
    if (invalid) { setFiles([]); setValidation("Sélectionnez des fichiers non vides aux formats PDF, DOCX, TXT, MD ou CSV."); return; }
    if(remote){const message=validateDocumentBatch(selected);if(message){setFiles([]);setValidation(message);return;}}
    setValidation(null); setFiles(selected);
  };
  const submit = async (event) => {
    event.preventDefault();
    if (!busy && !blocked && files.length && await onUpload(files)) onClose();
  };
  return <Modal open={open} onClose={() => { if (!busy) onClose(); }} title="Ajouter des documents">
    <form onSubmit={submit} className="kb-upload-form">
      <p>{remote ? "Formats : PDF, DOCX, TXT, MD, CSV. Jusqu’à 10 fichiers, 10 Mo par fichier et 49 Mo par lot. Le contenu est envoyé au serveur pour extraction et indexation RAG." : "Formats : PDF, DOCX, TXT, MD, CSV. En mode démo, seuls le nom, le type et la taille sont ajoutés. Le contenu n’est ni lu ni envoyé."}</p>
      <Input ref={input} type="file" multiple accept=".pdf,.docx,.txt,.md,.csv" label="Fichiers à ajouter"
        fieldClassName="sr-only" tabIndex={-1} disabled={busy || blocked} onChange={select} />
      <Button disabled={busy || blocked} onClick={() => input.current.click()}>Choisir des fichiers</Button>
      {files.length > 0 && <ul className="kb-file-list">{files.map((file, index) => <li key={`${file.name}-${index}`}>{file.name} · {formatSize(file.size)}</li>)}</ul>}
      {validation && <ErrorState message={validation} />}
      {error && <ErrorState message={error} />}
      <div className="kb-dialog-actions"><Button disabled={busy} onClick={onClose}>Annuler</Button>
        <Button type="submit" variant="primary" loading={busy} disabled={busy || blocked || !files.length}>Ajouter</Button></div>
    </form>
  </Modal>;
}
DocumentUpload.propTypes = { open: PropTypes.bool.isRequired, onClose: PropTypes.func.isRequired, onUpload: PropTypes.func.isRequired,
  busy: PropTypes.bool.isRequired, error: PropTypes.string, remote:PropTypes.bool, blocked:PropTypes.bool };
