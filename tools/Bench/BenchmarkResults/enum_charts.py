from __future__ import annotations

import math
from pathlib import Path
from typing import Dict, Iterable, List

import matplotlib.pyplot as plt
import pandas as pd


# Shared colors across all charts
METHOD_COLORS: Dict[str, str] = {
    "TryParse": "#4ECD72",  # green
    "TryFormat": "#4ECD72",  # keep Try* consistent
    "Reflection": "#45B7D1",  # blue
    "SourceGen": "#FF6B6B",  # coral
}

STYLE = {
	"light": {
		"text": "black",
		"edge": "black",
		"grid": "gray",
		"bg": (1, 1, 1, 0.1),
		"face": (1, 1, 1, 0.1),
	},
	"dark": {
		"text": "#E0E0E0",
		"edge": "#E0E0E0",
		"grid": "#E0E0E0",
		"bg": (0, 0, 0, 0.1),
		"face": (0, 0, 0, 0.1),
	},
}


def _parse_mean_ns(value: object) -> float:
	"""Return mean in nanoseconds as float."""
	if pd.isna(value):
		return math.nan
	if isinstance(value, str):
		value = value.replace(",", "").strip()  # allow csv with commas
	try:
		return float(value)
	except (TypeError, ValueError):
		return math.nan


def _prepare_dataframe(csv_path: Path) -> pd.DataFrame:
	df = pd.read_csv(csv_path)
	df["MeanNs"] = df["Mean"].apply(_parse_mean_ns)
	# million operations per second = 1e9 / ns / 1e6 = 1000 / ns
	df["Throughput"] = 1000.0 / df["MeanNs"]
	return df


def _slugify(parts: Iterable[str]) -> str:
	cleaned: List[str] = []
	for part in parts:
		token = str(part).strip().lower().replace(" ", "_")
		cleaned.append(token)
	return "_".join(cleaned)


def _method_color(method: str) -> str:
	base = method
	if method.startswith("Try"):
		base = "TryParse" if "Parse" in method else "TryFormat"
	return METHOD_COLORS.get(base, "#95A5A6")


def _create_chart(
	df: pd.DataFrame,
	title: str,
	mode: str,
	output_file: Path,
	label_col: str = "Method",
	hatch_col: str | None = None,
	sort_rows: bool = True,
) -> None:
	style = STYLE[mode]

	bars = df.sort_values("Throughput", ascending=True) if sort_rows else df

	fig, ax = plt.subplots(figsize=(8, 5), facecolor=style["face"])
	ax.set_facecolor(style["bg"])

	labels: List[str] = []
	for idx, row in enumerate(bars.itertuples()):
		method: str = getattr(row, "Method")
		label: str = getattr(row, label_col)
		hatch: str | None = getattr(row, hatch_col) if hatch_col and hasattr(row, hatch_col) else None
		color = _method_color(method)
		
		offset = 0

		if bars.__contains__('IgnoreCase'):
			offset = 0.1 if _as_bool(row.IgnoreCase) else -0.1

		bar = ax.barh(idx + offset, row.Throughput, color=color, edgecolor=style["edge"], linewidth=1, hatch=hatch)
		labels.append(label)
		ax.text(row.Throughput, idx, f" {row.Throughput:.1f}", va="center", ha="left",
				fontsize=9, fontweight="bold", color=style["text"])

	ax.set_yticks(range(len(labels)))
	ax.set_yticklabels(labels, color=style["text"])
	ax.set_xlabel("Throughput (million enums/s)", fontsize=11, fontweight="bold", color=style["text"])
	ax.set_title(title, fontsize=13, fontweight="bold", color=style["text"])
	ax.grid(axis="x", alpha=0.3, linestyle="--", color=style["grid"])
	ax.set_axisbelow(True)
	ax.tick_params(axis='x', colors=style["text"])
	for spine in ax.spines.values():
		spine.set_color(style["text"])

	output_file.parent.mkdir(parents=True, exist_ok=True)
	plt.tight_layout()
	plt.savefig(output_file, dpi=300, bbox_inches="tight", facecolor=style["face"])
	plt.close()
	print(f"Saved: {output_file}")


def _as_bool(value: object) -> bool:
	if isinstance(value, bool):
		return value
	if isinstance(value, (int, float)):
		return bool(value)
	return str(value).strip().lower() == "true"


def _render_parse_charts(df: pd.DataFrame, out_dir: Path) -> None:
	# Group by Bytes + ParseNumbers; keep IgnoreCase variants together in one chart
	unique_params = df[["Bytes", "ParseNumbers"]].drop_duplicates()
	for _, param_row in unique_params.iterrows():
		subset = df.copy()
		for col in ["Bytes", "ParseNumbers"]:
			subset = subset[subset[col] == param_row[col]]

		if subset.empty:
			continue

		bytes_flag = _as_bool(param_row["Bytes"])
		parse_numbers_flag = _as_bool(param_row["ParseNumbers"])

		encoding = "UTF8" if bytes_flag else "UTF16"
		value_label = "numbers" if parse_numbers_flag else "names"

		chart_df = subset.copy()
		chart_df["Label"] = chart_df.apply(
			lambda r: f"{r['Method']} ({'ignore case' if _as_bool(r['IgnoreCase']) else 'case-sensitive'})",
			axis=1,
		)
		chart_df["Hatch"] = chart_df["IgnoreCase"].apply(lambda v: "///" if _as_bool(v) else None)
		chart_df["IgnoreCaseBool"] = chart_df["IgnoreCase"].apply(_as_bool)

		# Order: fastest method (max throughput) at top, and within method put ignore-case first
		method_max = chart_df.groupby("Method")["Throughput"].max()
		method_rank = method_max.rank(method="first")
		chart_df["_method_rank"] = chart_df["Method"].map(method_rank)
		chart_df["_sub_rank"] = chart_df["IgnoreCaseBool"].apply(lambda v: 0 if v else 1)
		chart_df.sort_values(["_method_rank", "_sub_rank", "Throughput"], ascending=[True, True, False], inplace=True)

		title = f"Parse enum {value_label} from {encoding}"
		suffix = _slugify([value_label, encoding])

		_create_chart(
			chart_df,
			title,
			"light",
			out_dir / f"parse_enum_{suffix}_light.svg",
			label_col="Label",
			hatch_col="Hatch",
			sort_rows=False,
		)
		_create_chart(
			chart_df,
			title,
			"dark",
			out_dir / f"parse_enum_{suffix}_dark.svg",
			label_col="Label",
			hatch_col="Hatch",
			sort_rows=False,
		)


def _render_format_charts(df: pd.DataFrame, out_dir: Path) -> None:
	unique_params = df[["Numeric", "Bytes"]].drop_duplicates()
	for _, param_row in unique_params.iterrows():
		subset = df.copy()
		for col in ["Numeric", "Bytes"]:
			subset = subset[subset[col] == param_row[col]]

		if subset.empty:
			continue

		numeric_flag = _as_bool(param_row["Numeric"])
		bytes_flag = _as_bool(param_row["Bytes"])

		encoding = "UTF8" if bytes_flag else "UTF16"
		value_label = "values" if numeric_flag else "names"

		title = f"Format enum {value_label} to {encoding}"
		suffix = _slugify([value_label, encoding])

		_create_chart(subset, title, "light", out_dir / f"format_enum_{suffix}_light.svg")
		_create_chart(subset, title, "dark", out_dir / f"format_enum_{suffix}_dark.svg")


def main() -> None:
	base_dir = Path(__file__).resolve().parent
	enum_dir = base_dir / "Enums"

	parse_df = _prepare_dataframe(enum_dir / "Parse.csv")
	format_df = _prepare_dataframe(enum_dir / "Format.csv")

	_render_parse_charts(parse_df, enum_dir)
	_render_format_charts(format_df, enum_dir)

	print("\nAll enum charts generated successfully!")


if __name__ == "__main__":
	main()
