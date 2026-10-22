import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import Dashboard from "../src/components/Layout";
import { chartdata } from "../src/components/dataUtils";

// Isolate data/state orchestration from browser-only chart measurement.
vi.mock("../src/components/BarChart", () => ({ default: ({ data }) => <output aria-label="Bar chart">{JSON.stringify(data)}</output> }));
vi.mock("../src/components/AreaChart", () => ({ default: ({ data }) => <output aria-label="Area chart">{JSON.stringify(data)}</output> }));

describe("dashboard states", () => {
  it("preserves the demo data by default and passes caller data to both charts", () => {
    const view = render(<Dashboard />);
    expect(JSON.parse(screen.getByLabelText("Bar chart").textContent)).toEqual(chartdata);
    expect(JSON.parse(screen.getByLabelText("Area chart").textContent)).toEqual(chartdata);
    const data = [{ name: "Voyage confirmé", uv: 1, pv: 2, amt: 3 }];
    view.rerender(<Dashboard data={data} />);
    expect(JSON.parse(screen.getByLabelText("Bar chart").textContent)).toEqual(data);
    expect(JSON.parse(screen.getByLabelText("Area chart").textContent)).toEqual(data);
  });

  it("loading replaces stale charts and takes priority over a previous error or empty data", () => {
    const view = render(<Dashboard />);
    view.rerender(<Dashboard loading data={null} error="Erreur précédente" />);
    expect(screen.getAllByRole("status", { name: "Chargement…" })).toHaveLength(2);
    expect(screen.queryByLabelText("Bar chart")).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.queryByText("Aucune donnée")).toBeNull();
    view.rerender(<Dashboard />);
    expect(screen.queryByRole("status", { name: "Chargement…" })).toBeNull();
    expect(screen.getByLabelText("Area chart")).toBeTruthy();
  });

  it("empty data displays empty states instead of demo data or misleading charts", () => {
    render(<Dashboard data={[]} />);
    expect(screen.getAllByText("Aucune donnée disponible pour ce graphique.")).toHaveLength(2);
    expect(screen.queryByLabelText("Bar chart")).toBeNull();
    expect(screen.queryByLabelText("Area chart")).toBeNull();
  });

  it("errors offer a retry and do not expose technical exception messages", () => {
    const retry = vi.fn();
    const view = render(<Dashboard error={new Error("SQL connection string secret")} onRetry={retry} />);
    expect(screen.getAllByRole("alert")).toHaveLength(2);
    expect(screen.queryByText(/SQL connection/)).toBeNull();
    fireEvent.click(screen.getAllByRole("button", { name: "Réessayer" })[0]);
    expect(retry).toHaveBeenCalledOnce();
    view.rerender(<Dashboard error="Service temporairement indisponible" />);
    expect(screen.getAllByText("Service temporairement indisponible")).toHaveLength(2);
    expect(screen.queryByRole("button", { name: "Réessayer" })).toBeNull();
  });
});
