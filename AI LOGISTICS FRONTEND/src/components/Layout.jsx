import { useState } from "react";
import PropTypes from "prop-types";
import { Responsive, WidthProvider } from "react-grid-layout";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import DummyBarChart from "./BarChart";
import StackedAreaChart from "./AreaChart";
import DashboardWidget from "./DashboardWidget";
import { chartdata } from "./dataUtils";
import { chartDataType } from "./chartConfig";
import { tokens } from "../design-system";

const ResponsiveReactGridLayout = WidthProvider(Responsive);
const widgets = [
  { i: 0, x: 0, y: 0, w: 3, h: 2, Chart: DummyBarChart },
  { i: 1, x: 3, y: 0, w: 3, h: 2, Chart: StackedAreaChart },
];

const Layout = ({ data = chartdata, loading = false, error, onRetry, seriesNames, autoSize = false }) => {
  const [layouts, setLayouts] = useState(null);
  const chartData = data ?? [];
  return <div>
    <ResponsiveReactGridLayout onLayoutChange={setLayouts} layout={layouts}
      cols={tokens.grid.columns} breakpoints={tokens.grid.breakpoints} margin={tokens.grid.margin}
      compactType="vertical" autoSize={autoSize}>
      {widgets.map(({ Chart, ...widget }) => <div className="reactGridItem" key={widget.i}
        data-grid={{ ...widget, minW: 2, minH: 2, isDraggable: true, isResizable: true }}>
        <DashboardWidget loading={loading} empty={chartData.length === 0} error={error} onRetry={onRetry}>
          <Chart data={chartData} seriesNames={seriesNames} />
        </DashboardWidget>
      </div>)}
    </ResponsiveReactGridLayout>
  </div>;
};

Layout.propTypes = {
  data: chartDataType,
  loading: PropTypes.bool,
  error: DashboardWidget.propTypes.error,
  onRetry: PropTypes.func,
  seriesNames: PropTypes.objectOf(PropTypes.string),
  autoSize: PropTypes.bool,
};

export default Layout;
