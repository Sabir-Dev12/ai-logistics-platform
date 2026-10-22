import { forwardRef, useId } from "react";

const cx = (...parts) => parts.filter(Boolean).join(" ");

export function Spinner({ label = "Chargement", className = "" }) {
  return <span className={cx("ds-spinner", className)} role="status" aria-label={label}>
    <svg viewBox="0 0 20 20" aria-hidden="true"><circle cx="10" cy="10" r="6" /></svg>
  </span>;
}

export const Button = forwardRef(function Button({ children, variant = "secondary", loading = false,
  disabled = false, type = "button", className = "", ...props }, ref) {
  return <button {...props} ref={ref} type={type} disabled={disabled || loading}
    aria-busy={loading || undefined} data-variant={variant} className={cx("ds-button", className)}>
    {loading && <Spinner />}<span>{children}</span>
  </button>;
});

function Field({ label, hint, error, id, describedBy, children, className }) {
  const helpId = id + "-help";
  const description = cx(describedBy, (error || hint) && helpId) || undefined;
  return <div className={cx("ds-field", className)}>
    {label && <label htmlFor={id}>{label}</label>}
    {children({ id, "aria-invalid": error ? true : undefined, "aria-describedby": description })}
    {(error || hint) && <span id={helpId} className="ds-field-help" role={error ? "alert" : undefined}>{error || hint}</span>}
  </div>;
}

export const Input = forwardRef(function Input({ label, hint, error, id, className = "", fieldClassName,
  "aria-describedby": describedBy, ...props }, ref) {
  const generated = useId();
  return <Field label={label} hint={hint} error={error} id={id || generated} describedBy={describedBy} className={fieldClassName}>
    {(accessibility) => <input {...props} {...accessibility} ref={ref} className={cx("ds-input", className)} />}
  </Field>;
});

export const Select = forwardRef(function Select({ label, hint, error, id, children, className = "", fieldClassName,
  "aria-describedby": describedBy, ...props }, ref) {
  const generated = useId();
  return <Field label={label} hint={hint} error={error} id={id || generated} describedBy={describedBy} className={fieldClassName}>
    {(accessibility) => <select {...props} {...accessibility} ref={ref} className={cx("ds-select", className)}>{children}</select>}
  </Field>;
});

export const Card = forwardRef(function Card({ children, className = "", ...props }, ref) {
  return <div {...props} ref={ref} className={cx("ds-card", className)}>{children}</div>;
});

export function Badge({ children, variant = "info", className = "", ...props }) {
  return <span {...props} className={cx("ds-badge", className)} data-variant={variant}>{children}</span>;
}

export function Alert({ children, title, variant = "info", className = "", onDismiss, ...props }) {
  return <div {...props} className={cx("ds-alert", className)} data-variant={variant}
    role={variant === "error" || variant === "warning" ? "alert" : "status"}>
    <div>{title && <div className="ds-alert-title">{title}</div>}{children}</div>
    {onDismiss && <Button onClick={onDismiss} aria-label="Fermer l’alerte">×</Button>}
  </div>;
}

export function LoadingState({ label = "Chargement…", className = "" }) {
  return <div className={cx("ds-state", className)} aria-busy="true"><Spinner label={label} /><span>{label}</span></div>;
}

export function EmptyState({ title = "Aucune donnée", description, action, className = "" }) {
  return <div className={cx("ds-state", className)}><div>{title}</div>{description && <p>{description}</p>}{action}</div>;
}

export function ErrorState({ title = "Une erreur est survenue", message, onRetry, className = "" }) {
  return <Alert title={title} variant="error" className={className}>
    {message && <p>{message}</p>}{onRetry && <Button onClick={onRetry}>Réessayer</Button>}
  </Alert>;
}

export function Table({ columns, rows, rowKey = "id", caption, emptyState, loading = false, className = "" }) {
  if (!columns?.length) throw new Error("Table requires at least one column.");
  return <div className={cx("ds-table-container", className)} aria-busy={loading || undefined}>
    <table className="ds-table">
      {caption && <caption>{caption}</caption>}
      <thead><tr>{columns.map((column) => <th key={column.key} scope="col">{column.header}</th>)}</tr></thead>
      <tbody>
        {loading ? <tr><td colSpan={columns.length}><LoadingState /></td></tr>
          : !rows.length ? <tr><td colSpan={columns.length}>{emptyState || <EmptyState />}</td></tr>
            : rows.map((row) => <tr key={typeof rowKey === "function" ? rowKey(row) : row[rowKey]}>
              {columns.map((column) => <td key={column.key}>{column.render ? column.render(row[column.key], row) : row[column.key] ?? "—"}</td>)}
            </tr>)}
      </tbody>
    </table>
  </div>;
}

export function Pagination({ page, totalPages, onPageChange, disabled = false, className = "" }) {
  const last = Math.max(1, totalPages);
  const current = Math.min(last, Math.max(1, page));
  const first = Math.max(1, Math.min(current - 2, last - 4));
  const pages = Array.from({ length: Math.min(5, last) }, (_, i) => first + i);
  return <nav className={cx("ds-pagination", className)} aria-label="Pagination">
    <Button disabled={disabled || current === 1} onClick={() => onPageChange(current - 1)}>Précédent</Button>
    {pages.map((number) => <Button key={number} disabled={disabled} aria-current={number === current ? "page" : undefined}
      variant={number === current ? "primary" : "secondary"} onClick={() => onPageChange(number)}>{number}</Button>)}
    <Button disabled={disabled || current === last} onClick={() => onPageChange(current + 1)}>Suivant</Button>
  </nav>;
}
