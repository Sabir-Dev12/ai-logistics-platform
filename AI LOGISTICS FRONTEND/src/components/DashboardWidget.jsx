import PropTypes from "prop-types";
import { Card, EmptyState, ErrorState, LoadingState } from "../design-system";

export default function DashboardWidget({ children, loading = false, empty = false, error, onRetry }) {
  return <Card className="h-full w-full">
    {loading ? <LoadingState />
      : error ? <ErrorState message={typeof error === "string" ? error : "Impossible de charger le graphique."} onRetry={onRetry} />
        : empty ? <EmptyState description="Aucune donnée disponible pour ce graphique." />
          : children}
  </Card>;
}

DashboardWidget.propTypes = {
  children: PropTypes.node,
  loading: PropTypes.bool,
  empty: PropTypes.bool,
  error: PropTypes.oneOfType([PropTypes.string, PropTypes.bool, PropTypes.instanceOf(Error)]),
  onRetry: PropTypes.func,
};
