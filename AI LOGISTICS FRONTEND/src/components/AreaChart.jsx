import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import { chartdata } from "./dataUtils";
import { chartAxisProps, chartColors, chartDataType, chartTooltipProps } from "./chartConfig";
import PropTypes from "prop-types";

const StackedAreaChart = ({ data = chartdata, seriesNames }) => (
  <ResponsiveContainer width="100%" height="100%">
    <AreaChart
      data={data}
      margin={{
        top: 10,
        right: 30,
        left: 0,
        bottom: 0,
      }}
    >
      <CartesianGrid stroke={chartColors.border} strokeDasharray="3 3" />
      <XAxis
        {...chartAxisProps}
        dataKey="name"
        textAnchor="end"
      />
      <YAxis {...chartAxisProps} />
      <Tooltip {...chartTooltipProps} />
      <Area
        type="monotone"
        dataKey="uv"
        name={seriesNames?.uv}
        stackId="1"
        stroke={chartColors.chartViolet}
        fill={chartColors.chartViolet}
      />
      <Area
        type="monotone"
        dataKey="pv"
        name={seriesNames?.pv}
        stackId="1"
        stroke={chartColors.chartGreen}
        fill={chartColors.chartGreen}
      />
      <Area
        type="monotone"
        dataKey="amt"
        name={seriesNames?.amt}
        stackId="1"
        stroke={chartColors.chartYellow}
        fill={chartColors.chartYellow}
      />
    </AreaChart>
  </ResponsiveContainer>
);

StackedAreaChart.propTypes = { data: chartDataType, seriesNames: PropTypes.objectOf(PropTypes.string) };
export default StackedAreaChart;
