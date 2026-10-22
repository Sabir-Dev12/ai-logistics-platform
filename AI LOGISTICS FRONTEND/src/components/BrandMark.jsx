import PropTypes from "prop-types";

export default function BrandMark({ size = 24, className = "" }) {
  return <svg className={className} width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
    {Array.from({ length: 12 }, (_, index) => <path key={index} d="M12 2v5.2" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" transform={`rotate(${index * 30} 12 12)`} />)}
    <circle cx="12" cy="12" r="2" fill="currentColor" />
  </svg>;
}
BrandMark.propTypes = { size: PropTypes.number, className: PropTypes.string };
