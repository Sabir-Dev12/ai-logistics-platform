import { useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button, Card } from "./primitives";
import { useLayer } from "./useLayer";
import { X } from "lucide-react";

export function Modal({ open, onClose, title, children, actions, className = "" }) {
  const id = useId();
  const rootRef = useRef(null);
  useLayer({ open, onDismiss: onClose, rootRef, modal: true });
  if (!open) return null;
  return createPortal(<div className="ds-modal-backdrop">
    <Card ref={rootRef} className={`ds-modal ${className}`} role="dialog" aria-modal="true" aria-labelledby={id} tabIndex={-1}>
      <div className="ds-modal-header"><h2 id={id}>{title}</h2><Button onClick={onClose} aria-label="Fermer la fenêtre"><X size={18} aria-hidden="true" /></Button></div>
      <div className="ds-modal-content">{children}</div>
      {actions && <div className="ds-modal-actions">{actions}</div>}
    </Card>
  </div>, document.body);
}

export function Dropdown({ label, items, disabled = false, className = "" }) {
  const [open, setOpen] = useState(false);
  const id = useId();
  const rootRef = useRef(null);
  const triggerRef = useRef(null);
  useLayer({ open, onDismiss: () => setOpen(false), rootRef, triggerRef });
  const move = (event) => {
    if (!["ArrowDown", "ArrowUp", "Home", "End", "Tab"].includes(event.key)) return;
    if (event.key === "Tab") { setOpen(false); return; }
    event.preventDefault();
    const buttons = [...rootRef.current.querySelectorAll("[role='menuitem']:not(:disabled)")];
    if (!buttons.length) return;
    const index = buttons.indexOf(document.activeElement);
    const next = event.key === "Home" ? 0 : event.key === "End" ? buttons.length - 1
      : (index + (event.key === "ArrowUp" ? -1 : 1) + buttons.length) % buttons.length;
    buttons[next].focus();
  };
  return <div className={`ds-dropdown ${className}`}>
    <Button ref={triggerRef} disabled={disabled} aria-haspopup="menu" aria-expanded={open} aria-controls={open ? id : undefined}
      onClick={() => setOpen(!open)} onKeyDown={(event) => {
        if (event.key === "ArrowDown" || event.key === "ArrowUp") { event.preventDefault(); setOpen(true); }
      }}>{label}</Button>
    {open && <div ref={rootRef} id={id} className="ds-dropdown-menu ds-card" role="menu" aria-label={label} tabIndex={-1} onKeyDown={move}>
      {items.map((item) => <button key={item.id} type="button" role="menuitem" disabled={item.disabled}
        className="ds-dropdown-item" onClick={() => { setOpen(false); item.onSelect?.(); }}>{item.label}</button>)}
    </div>}
  </div>;
}
