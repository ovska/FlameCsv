import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.colors as mcolors
import numpy as np
from pathlib import Path
from itertools import product

# Configuration
LIBRARY_COLORS = {
    'FlameCsv': '#FF6B6B',
    'Sep': "#4ECD72",
    'Sylvan': '#45B7D1',
    'CsvHelper': "#EDFF7A",
    'RecordParser': "#D898D6"
}

def adjust_color_lightness(hex_color, factor):
    """Adjust color lightness. factor > 1 lightens, factor < 1 darkens."""
    rgb = mcolors.hex2color(hex_color)
    # Convert to HLS, adjust lightness, convert back
    import colorsys
    h, l, s = colorsys.rgb_to_hls(*rgb)
    l = max(0, min(1, l * factor))
    r, g, b = colorsys.hls_to_rgb(h, l, s)
    return mcolors.rgb2hex((r, g, b))

CPU_SUBTITLES = {
    'AVX2': 'AMD Ryzen 7 3700X',
    'ARM': 'Apple M4 Max 16c',
    'Neon': 'Apple M4 Max 16c',
    'AVX512': 'AMD Ryzen 7 PRO 7840U'
}

# Directories to process
BENCHMARK_DIRS = ['AVX2', 'Neon']

# Define benchmark configurations
# Each config specifies: filepath, title, throughput_value, throughput_unit, and parameters
# Parameters is a dict where key = column name, value = dict of {value: display_name}
# throughput_value can be a number or a dict like {'Quoted': {'False': 8.2, 'True': 17.2}} for per-parameter values
BENCHMARK_CONFIGS = [
    {
        "filepath": "FlameCsv.Benchmark.Comparisons.EnumerateBench-report.csv",
        "title": "Enumerating CSV fields",
        "throughput_value": {"Quoted": {"False": 65536, "True": 100000}},  # Number of records
        "throughput_unit": "million records/s",
        "throughput_divisor": 1_000_000,
        "decimal_places": 2,
        "parameters": {
            "Quoted": {"False": "Unquoted", "True": "Quoted"},
            "Async": {"False": "Sync", "True": "Async"},
        },
    },
    {
        "filepath": "FlameCsv.Benchmark.Comparisons.WriteObjects-report.csv",
        "title": "Writing objects to CSV",
        "throughput_value": 20000,  # Number of records
        "throughput_unit": "million records/s",
        "throughput_divisor": 1_000_000,  # Divide result by this value for display
        "decimal_places": 2,
        "parameters": {"Async": {"False": "Sync", "True": "Async"}},
    },
    {
        "filepath": "FlameCsv.Benchmark.Comparisons.PeekFields-report.csv",
        "title": "Sum the value of one column",
        "throughput_value": 65536,  # Number of records
        "throughput_unit": "million records/s",
        "throughput_divisor": 1_000_000,  # Divide result by this value for display
        "decimal_places": 2,
        "parameters": {},
    },
    {
        "filepath": "FlameCsv.Benchmark.Comparisons.ReadObjects-report.csv",
        "title": "Reading objects from CSV",
        "throughput_value": 20000,  # Number of records
        "throughput_unit": "million records/s",
        "throughput_divisor": 1_000_000,
        "decimal_places": 2,
        "parameters": {"Async": {"False": "Sync", "True": "Async"}},
    },
]

def parse_benchmark_results(filepath, param_columns):
    """Parse CSV from BenchmarkDotNet results"""
    # Read CSV file
    df = pd.read_csv(filepath)
    
    # Detect time column and unit
    if 'Mean [ms]' in df.columns:
        time_col = 'Mean [ms]'
        time_divisor = 1000  # ms to seconds
    elif 'Mean [μs]' in df.columns:
        time_col = 'Mean [μs]'
        time_divisor = 1_000_000  # μs to seconds
    elif 'Mean [us]' in df.columns:
        time_col = 'Mean [us]'
        time_divisor = 1_000_000  # μs to seconds
    elif 'Mean [ns]' in df.columns:
        time_col = 'Mean [ns]'
        time_divisor = 1_000_000_000  # ns to seconds
    else:
        raise ValueError(f"Could not find Mean time column in {filepath}. Columns: {list(df.columns)}")
    
    # Detect allocated memory column and unit
    alloc_col = None
    alloc_divisor = 1  # to convert to MB
    if 'Allocated [MB]' in df.columns:
        alloc_col = 'Allocated [MB]'
        alloc_divisor = 1
    elif 'Allocated [kB]' in df.columns:
        alloc_col = 'Allocated [kB]'
        alloc_divisor = 1024  # kB to MB
    elif 'Allocated [B]' in df.columns:
        alloc_col = 'Allocated [B]'
        alloc_divisor = 1024 * 1024  # B to MB
    
    # Select only the columns we need
    columns_to_select = ['Method', time_col] + list(param_columns)
    if alloc_col:
        columns_to_select.append(alloc_col)
    df = df[columns_to_select].copy()
    
    # Trim leading underscore from Method names
    df['Method'] = df['Method'].str.lstrip('_')
    
    # Convert parameter columns to string format to match filtering
    for col in param_columns:
        df[col] = df[col].astype(str)
    
    # Parse mean time (convert to seconds)
    def parse_time(time_val):
        if pd.isna(time_val) or time_val == 'NA':
            return np.nan
        try:
            # Handle quoted values with thousand separators (e.g., "24,387.6")
            if isinstance(time_val, str):
                time_val = time_val.replace(',', '').strip('"')
            return float(time_val) / time_divisor
        except (ValueError, TypeError):
            return np.nan
    
    df['MeanSeconds'] = df[time_col].apply(parse_time)
    
    # Parse allocated memory (convert to MB)
    def parse_alloc(alloc_val):
        if pd.isna(alloc_val) or alloc_val == 'NA':
            return np.nan
        try:
            if isinstance(alloc_val, str):
                alloc_val = alloc_val.replace(',', '').strip('"')
            return float(alloc_val) / alloc_divisor
        except (ValueError, TypeError):
            return np.nan
    
    if alloc_col:
        df['AllocatedMB'] = df[alloc_col].apply(parse_alloc)
    else:
        df['AllocatedMB'] = np.nan
    
    return df

def create_throughput_chart(df, param_filters, output_file, throughput_value=100, throughput_unit='MB/s', throughput_divisor=1, decimal_places=1, mode='light', subtitle=None, title='Benchmark'):
    """Create a bar chart showing throughput
    
    param_filters: dict of {column_name: (value, display_name)}
    throughput_value: Value to divide by mean time (e.g., file size in MB, record count)
    throughput_unit: Label for the throughput unit (e.g., 'MB/s', 'records/s')
    throughput_divisor: Divide the calculated throughput by this value for display (e.g., 1_000_000 for millions)
    decimal_places: Number of decimal places to show in value labels
    """
    # Apply all parameter filters
    filtered = df.copy()
    for col, (value, _) in param_filters.items():
        filtered = filtered[filtered[col] == value]
    filtered = filtered[filtered['MeanSeconds'].notna()]

    if filtered.empty:
        return

    # Calculate throughput (and apply divisor for display units)
    filtered['Throughput'] = (throughput_value / filtered['MeanSeconds']) / throughput_divisor

    # Check if there are parallel versions
    filtered['_is_parallel'] = filtered['Method'].str.contains('_Parallel')
    has_parallel = filtered['_is_parallel'].any()
    has_non_parallel = (~filtered['_is_parallel']).any()

    # Sort: non-parallel first (by throughput desc), then parallel (by throughput desc)
    # For horizontal bar chart, we want highest at top, so ascending=True reverses it
    if has_parallel and has_non_parallel:
        non_parallel = filtered[~filtered['_is_parallel']].sort_values('Throughput', ascending=True)
        parallel = filtered[filtered['_is_parallel']].sort_values('Throughput', ascending=True)
        # Non-parallel will be at top (drawn last), separator, then parallel at bottom
        filtered = pd.concat([parallel, non_parallel], ignore_index=True)
    else:
        filtered = filtered.sort_values('Throughput', ascending=True)

    # Configure colors based on mode
    if mode == 'dark':
        text_color = '#E0E0E0'
        edge_color = '#E0E0E0'
        grid_color = '#E0E0E0'
        bg_color = (0, 0, 0, 0.1)  # translucent black
        fig_facecolor = (0, 0, 0, 0.1)
        annotation_bg = (1, 1, 1, 0.08)
        annotation_bg_highlight = (0.55, 1.0, 0.65, 0.35)  # soft green for minima
    else:  # light mode
        text_color = 'black'
        edge_color = 'black'
        grid_color = 'gray'
        bg_color = (1, 1, 1, 0.1)  # translucent white
        fig_facecolor = (1, 1, 1, 0.1)
        annotation_bg = (0, 0, 0, 0.05)
        annotation_bg_highlight = (0.5, 0.85, 0.6, 0.45)  # soft green for minima

    fig, ax = plt.subplots(figsize=(10, 6), facecolor=fig_facecolor)
    ax.set_facecolor(bg_color)

    # Build y-positions and labels, inserting separator if needed
    y_labels = []
    y_positions = []
    throughputs = []
    bar_data = []  # (display_name, throughput, color, hatch, is_separator)

    # Track where to insert separator
    separator_inserted = False
    parallel_count = filtered['_is_parallel'].sum() if has_parallel and has_non_parallel else 0

    for i, (idx, row) in enumerate(filtered.iterrows()):
        # Insert separator between parallel and non-parallel groups
        if has_parallel and has_non_parallel and not separator_inserted and i == parallel_count:
            bar_data.append(('', 0, 'none', None, True, np.nan))  # separator
            separator_inserted = True

        method = row['Method']
        is_parallel = '_Parallel' in method

        # Build display name with proper formatting
        base_method = method.replace('_Parallel', '')

        # Handle FlameCsv variants (Flame_SrcGen, Flame_Reflection, FlameCsv_SrcGen, FlameCsv_Reflection)
        if base_method.startswith('Flame_') or base_method.startswith('FlameCsv_'):
            # Remove prefix to get variant
            if base_method.startswith('FlameCsv_'):
                variant = base_method.replace('FlameCsv_', '')
            else:
                variant = base_method.replace('Flame_', '')

            if variant == 'SrcGen':
                display_name = 'FlameCsv SourceGen'
            elif variant == 'Reflection':
                display_name = 'FlameCsv Reflection'
            else:
                display_name = f'FlameCsv {variant}'
            color_key = 'FlameCsv'
        # Handle _Hardcoded suffix for other libraries
        elif '_Hardcoded' in base_method:
            clean_name = base_method.replace('_Hardcoded', '')
            display_name = clean_name
            color_key = clean_name
        elif base_method == 'FlameCsv' and 'Reading objects' in title:
            display_name = 'FlameCsv Reflection'
            color_key = 'FlameCsv'
        else:
            display_name = base_method
            color_key = base_method

        color = LIBRARY_COLORS.get(color_key, '#95A5A6')

        # Adjust color for parallel versions
        if is_parallel:
            color = adjust_color_lightness(color, 0.9)

        hatch = None
        if is_parallel:
            hatch = 'oo'

        alloc_mb = row.get('AllocatedMB', np.nan)
        bar_data.append((display_name, row['Throughput'], color, hatch, False, alloc_mb))

    # Create bars from bar_data
    bars = []
    labels = []
    separator_y = None
    for i, (display_name, throughput, color, hatch, is_separator, alloc_mb) in enumerate(bar_data):
        if is_separator:
            # Add empty space for separator
            labels.append('')
            bar = ax.barh(i, 0, color='none', edgecolor='none')
            separator_y = i
        else:
            labels.append(display_name)
            bar = ax.barh(i, throughput, 
                          color=color, hatch=hatch, edgecolor=edge_color, linewidth=1)
        bars.append(bar)

    # Add horizontal line at separator position
    if separator_y is not None:
        ax.axhline(y=separator_y, color=text_color, linestyle='--', linewidth=0.8, alpha=0.5)
        # Label parallel section to the left of the separator line
        ax.text(-0.02, separator_y, 'Parallel', transform=ax.get_yaxis_transform(),
                ha='right', va='center', fontsize=10, fontweight='bold',
                color=text_color, clip_on=False)

    # Set y-tick positions and labels
    ax.set_yticks(range(len(bar_data)))
    ax.set_yticklabels(labels)

    # Styling
    ax.set_xlabel(f'Throughput ({throughput_unit})', fontsize=12, fontweight='bold', color=text_color)

    # Build title with parameter display names
    param_labels = [display_name for _, (_, display_name) in param_filters.items()]
    param_str = ', '.join(param_labels)
    full_title = title
    if param_str:
        full_title += f" ({param_str})"
    if subtitle:
        full_title += f'\n{subtitle}'
    ax.set_title(full_title, fontsize=14, fontweight='bold', color=text_color)
    ax.grid(axis='x', alpha=0.3, linestyle='--', color=grid_color)
    ax.set_axisbelow(True)

    # Set tick colors
    ax.tick_params(axis='both', colors=text_color)
    for spine in ax.spines.values():
        spine.set_color(text_color)

    # Get max throughput for positioning
    max_throughput = max(t for _, t, _, _, is_sep, _ in bar_data if not is_sep)

    # Add value labels (throughput right after bar)
    for i, (display_name, throughput, color, hatch, is_separator, alloc_mb) in enumerate(bar_data):
        if not is_separator:
            ax.text(throughput, i, f" {throughput:.{decimal_places}f}", 
                    va='center', fontsize=10, fontweight='bold', color=text_color)

    # Add memory labels outside the chart on the right
    # Use axes transform to place text at fixed position relative to axes
    alloc_values = [alloc_mb for _, _, _, _, is_sep, alloc_mb in bar_data if not is_sep and not pd.isna(alloc_mb)]
    min_alloc = min(alloc_values) if alloc_values else None

    for i, (display_name, throughput, color, hatch, is_separator, alloc_mb) in enumerate(bar_data):
        if not is_separator and not pd.isna(alloc_mb):
            if alloc_mb >= 1:
                mem_str = f"{alloc_mb:.1f} MB"
            elif alloc_mb * 1024 >= 1:
                mem_str = f"{alloc_mb * 1024:.0f} KB"
            else:
                mem_str = f"{alloc_mb * 1024 * 1024:.0f} B"

            is_min_alloc = min_alloc is not None and np.isclose(alloc_mb, min_alloc)
            highlight_bg = annotation_bg_highlight if is_min_alloc else annotation_bg
            font_weight = 'bold' if is_min_alloc else 'normal'

            # Position outside chart area, right-aligned to a fixed column
            ax.annotate(mem_str, xy=(1.12, i), xycoords=('axes fraction', 'data'),
                       va='center', ha='right', fontsize=9, fontweight=font_weight,
                       color=text_color, alpha=0.9,
                       bbox=dict(boxstyle='round,pad=0.25', facecolor=highlight_bg, edgecolor='none'),
                       annotation_clip=False)

    plt.tight_layout()
    plt.savefig(output_file, dpi=300, bbox_inches='tight', facecolor=fig_facecolor)
    plt.close()
    print(f"Saved: {output_file}")

def main():
    for benchmark_dir in BENCHMARK_DIRS:
        print(f"\nProcessing {benchmark_dir}...")
        
        for config in BENCHMARK_CONFIGS:
            filepath = f"{benchmark_dir}/{config['filepath']}"
            title = config['title']
            throughput_value_config = config['throughput_value']
            throughput_unit = config['throughput_unit']
            throughput_divisor = config.get('throughput_divisor', 1)  # Default to 1 (no scaling)
            decimal_places = config.get('decimal_places', 1)  # Default to 1 decimal place
            parameters = config['parameters']
            
            # Check if file exists
            if not Path(filepath).exists():
                print(f"  Skipping {filepath} (file not found)")
                continue
            
            # Determine subtitle based on folder
            subtitle = CPU_SUBTITLES.get(benchmark_dir)
            
            # Parse results from the benchmark file
            df = parse_benchmark_results(filepath, list(parameters.keys()))

            # Exclude Sep hardcoded variants from ReadObjects charts
            if config["filepath"].endswith("ReadObjects-report.csv"):
                sep_hardcoded = df['Method'].str.contains('Sep', regex=False) & df['Method'].str.contains('Hardcoded', regex=False)
                df = df[~sep_hardcoded]
            
            # Always load memory data from Neon (has complete data)
            neon_filepath = f"Neon/{config['filepath']}"
            if Path(neon_filepath).exists() and benchmark_dir != 'Neon':
                neon_df = parse_benchmark_results(neon_filepath, list(parameters.keys()))
                if config["filepath"].endswith("ReadObjects-report.csv"):
                    sep_hardcoded_neon = neon_df['Method'].str.contains('Sep', regex=False) & neon_df['Method'].str.contains('Hardcoded', regex=False)
                    neon_df = neon_df[~sep_hardcoded_neon]
                # Merge memory data from Neon into main df
                # Create a key from Method + parameters for matching
                param_cols = list(parameters.keys())
                merge_cols = ['Method'] + param_cols
                
                # Normalize method names for matching (handle naming differences between datasets)
                def normalize_method(m):
                    # _FlameCsv_Reflection -> _FlameCsv, _FlameCsv -> _FlameCsv
                    # But keep _Flame_SrcGen, _FlameCsv_SrcGen_Parallel etc.
                    if m == 'FlameCsv_Reflection':
                        return 'FlameCsv'
                    return m
                
                if 'AllocatedMB' in neon_df.columns:
                    memory_data = neon_df[merge_cols + ['AllocatedMB']].copy()
                    # Add normalized method column for matching
                    df['_norm_method'] = df['Method'].apply(normalize_method)
                    memory_data['_norm_method'] = memory_data['Method'].apply(normalize_method)
                    
                    # Merge on normalized method + params
                    norm_merge_cols = ['_norm_method'] + param_cols
                    memory_data = memory_data.drop(columns=['Method']).rename(columns={'_norm_method': '_norm_method'})
                    df = df.drop(columns=['AllocatedMB'], errors='ignore')
                    df = df.merge(memory_data[norm_merge_cols + ['AllocatedMB']], on=norm_merge_cols, how='left')
                    df = df.drop(columns=['_norm_method'])
            
            # Generate all combinations of parameter values
            param_names = list(parameters.keys())
            param_value_lists = [list(parameters[name].keys()) for name in param_names]
            
            for param_values in product(*param_value_lists):
                # Build param_filters dict: {column: (value, display_name)}
                param_filters = {}
                for name, value in zip(param_names, param_values):
                    param_filters[name] = (value, parameters[name][value])
                
                # Resolve throughput_value (can be a number or a dict keyed by parameter values)
                if isinstance(throughput_value_config, dict):
                    # Find the first matching parameter key
                    throughput_value = None
                    for param_name, param_val in zip(param_names, param_values):
                        if param_name in throughput_value_config:
                            throughput_value = throughput_value_config[param_name][param_val]
                            break
                    if throughput_value is None:
                        raise ValueError(f"Could not resolve throughput_value for {param_filters}")
                else:
                    throughput_value = throughput_value_config
                
                # Build filename suffix from display names (lowercase, underscores)
                suffix_parts = [parameters[name][value].lower().replace(' ', '_') 
                               for name, value in zip(param_names, param_values)]
                suffix = '_'.join(suffix_parts)
                
                # Generate base filename from benchmark title
                base_name = title.lower().replace(' ', '_')
                
                # Use the same folder as the input CSV for output
                input_path = Path(filepath)
                output_dir = input_path.parent
                
                # Light mode version
                output_file_light = output_dir / f'{base_name}_{suffix}_light.svg'
                create_throughput_chart(df, param_filters, output_file_light, throughput_value, 
                                       throughput_unit, throughput_divisor, decimal_places, mode='light', subtitle=subtitle, title=title)
                
                # Dark mode version
                output_file_dark = output_dir / f'{base_name}_{suffix}_dark.svg'
                create_throughput_chart(df, param_filters, output_file_dark, throughput_value, 
                                       throughput_unit, throughput_divisor, decimal_places, mode='dark', subtitle=subtitle, title=title)
    
    print("\nAll charts generated successfully!")

if __name__ == '__main__':
    main()
