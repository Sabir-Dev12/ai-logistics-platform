import PropTypes from "prop-types";
import { tokens } from "../design-system/tokens";

export const chartColors = tokens.colors;
export const chartAxisProps = {
  className: "text-[10px]",
  tickLine: false,
  axisLine: { stroke: tokens.colors.axis, strokeWidth: 0.3 },
  tick: { fill: tokens.colors.muted, fontFamily: tokens.typography.widget, fontSize: 10 },
};
export const chartTooltipProps = { contentStyle: { background: tokens.colors.surfaceRaised, border: `1px solid ${tokens.colors.border}`, borderRadius: 10, color: tokens.colors.text }, labelStyle: { color: tokens.colors.text } };
export const chartDataType = PropTypes.arrayOf(PropTypes.shape({
  name: PropTypes.string.isRequired,
  uv: PropTypes.number.isRequired,
  pv: PropTypes.number.isRequired,
  amt: PropTypes.number.isRequired,
}));
