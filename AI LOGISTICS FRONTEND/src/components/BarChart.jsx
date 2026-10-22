import {
  BarChart,
  Bar,
  Rectangle,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from "recharts";
import { chartdata } from "./dataUtils";
import { chartAxisProps, chartColors, chartDataType, chartTooltipProps } from "./chartConfig";
import PropTypes from "prop-types";

const DummyBarChart = ({ data = chartdata, seriesNames }) => {
  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart
        data={data}
        margin={{
          top: 5,
          right: 30,
          left: 20,
          bottom: 5,
        }}
      >
        <CartesianGrid
          stroke={chartColors.border}
          vertical={false}
          strokeWidth={0.45}
          opacity={0.5}
          strokeDasharray="3 3 "
        />
        <XAxis
          {...chartAxisProps}
          dataKey="name"
          textAnchor="end"
        />
        <YAxis {...chartAxisProps} />
        <Tooltip {...chartTooltipProps} />
        <Legend
          wrapperStyle={{
            bottom: 5,
          }}
        />
        <Bar
          dataKey="pv"
          name={seriesNames?.pv}
          fill={chartColors.chartViolet}
          activeBar={<Rectangle fill={chartColors.activePink} stroke={chartColors.activeBlue} />}
        />
        <Bar
          dataKey="uv"
          name={seriesNames?.uv}
          fill={chartColors.chartGreen}
          activeBar={<Rectangle fill={chartColors.activeGold} stroke={chartColors.activePurple} />}
        />
      </BarChart>
    </ResponsiveContainer>
  );
};

DummyBarChart.propTypes = { data: chartDataType, seriesNames: PropTypes.objectOf(PropTypes.string) };
export default DummyBarChart;
