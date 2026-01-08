import pandas as pd
import matplotlib.pyplot as plt
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

CPU_SUBTITLES = {
    'AVX2': 'AMD Ryzen 7 3700X',
    'ARM': 'Apple M4 Max 16c',
    'AVX512': 'AMD Ryzen 7 PRO 7840U'
}

# Define benchmark configurations
# Each config specifies: filepath, title, throughput_value, throughput_unit, and parameters
# Parameters is a dict where key = column name, value = dict of {value: display_name}
BENCHMARK_CONFIGS = [
    {
        "filepath": "AVX2/FlameCsv.Benchmark.Comparisons.EnumerateBench-report.csv",
        "title": "Enumerating CSV fields",
        "throughput_value": 8.2,  # Value to divide by mean time
        "throughput_unit": "MB/s",  # Unit label (e.g., 'MB/s', 'records/s', 'ops/s')
        "decimal_places": 0,
        "parameters": {
            "Quoted": {"False": "Unquoted", "True": "Quoted"},
            "Async": {"False": "Sync", "True": "Async"},
        },
    },
    {
        "filepath": "AVX2/FlameCsv.Benchmark.Comparisons.WriteObjects-report.csv",
        "title": "Writing objects to CSV",
        "throughput_value": 20000,  # Number of records
        "throughput_unit": "million records/s",
        "throughput_divisor": 1_000_000,  # Divide result by this value for display
        "decimal_places": 2,
        "parameters": {"Async": {"False": "Sync", "True": "Async"}},
    },
    {
        "filepath": "AVX2/FlameCsv.Benchmark.Comparisons.PeekFields-report.csv",
        "title": "Sum the value of one column",
        "throughput_value": 65536,  # Number of records
        "throughput_unit": "million records/s",
        "throughput_divisor": 1_000_000,  # Divide result by this value for display
        "decimal_places": 2,
        "parameters": {},
    },
]

def parse_benchmark_results(filepath, param_columns):
    """Parse CSV from BenchmarkDotNet results"""
    # Read CSV file
    df = pd.read_csv(filepath)
    
    # Select only the columns we need
    columns_to_select = ['Method', 'Mean [ms]'] + list(param_columns)
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
            return float(time_val) / 1000  # CSV has values in ms
        except (ValueError, TypeError):
            return np.nan
    
    df['MeanSeconds'] = df['Mean [ms]'].apply(parse_time)
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

    # Sort by throughput descending
    filtered = filtered.sort_values('Throughput', ascending=True)

    # Configure colors based on mode
    if mode == 'dark':
        text_color = '#E0E0E0'
        edge_color = '#E0E0E0'
        grid_color = '#E0E0E0'
        bg_color = 'none'
        fig_facecolor = 'none'
    else:  # light mode
        text_color = 'black'
        edge_color = 'black'
        grid_color = 'gray'
        bg_color = 'none'
        fig_facecolor = 'none'

    fig, ax = plt.subplots(figsize=(10, 6), facecolor=fig_facecolor)
    ax.set_facecolor(bg_color)

    # Create bars with consistent colors and styles
    bars = []
    for idx, row in filtered.iterrows():
        method = row['Method']
        is_parallel = '_Parallel' in method

        # Build display name with proper formatting
        base_method = method.replace('_Parallel', '')

        # Handle FlameCsv variants (Flame_SrcGen, Flame_Reflection)
        if base_method.startswith('Flame_'):
            variant = base_method.replace('Flame_', '')
            if variant == 'SrcGen':
                display_name = 'FlameCsv SourceGen'
            elif variant == 'Reflection':
                display_name = 'FlameCsv Reflection'
            else:
                display_name = f'FlameCsv {variant}'
            color_key = 'FlameCsv'
        else:
            display_name = base_method
            color_key = base_method

        # Add parallel suffix
        if is_parallel:
            display_name += ' (Parallel)'

        color = LIBRARY_COLORS.get(color_key, '#95A5A6')

        hatch = None
        is_async = any(col == 'Async' and val == 'True' for col, (val, _) in param_filters.items())
        if is_parallel and is_async:
            hatch = '...///' # Combined hatch for both
        elif is_parallel:
            hatch = '...'
        elif is_async:
            hatch = '///'

        bar = ax.barh(display_name, row['Throughput'], 
                      color=color, hatch=hatch, edgecolor=edge_color, linewidth=1)
        bars.append(bar)

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

    # Add value labels
    for i, (idx, row) in enumerate(filtered.iterrows()):
        ax.text(row['Throughput'], i, f" {row['Throughput']:.{decimal_places}f}", 
                va='center', fontsize=10, fontweight='bold', color=text_color)

    plt.tight_layout()
    plt.savefig(output_file, dpi=300, bbox_inches='tight', transparent=True)
    plt.close()
    print(f"Saved: {output_file}")

def main():
    for config in BENCHMARK_CONFIGS:
        filepath = config['filepath']
        title = config['title']
        throughput_value = config['throughput_value']
        throughput_unit = config['throughput_unit']
        throughput_divisor = config.get('throughput_divisor', 1)  # Default to 1 (no scaling)
        decimal_places = config.get('decimal_places', 1)  # Default to 1 decimal place
        parameters = config['parameters']
        
        # Determine subtitle based on folder
        subtitle = None
        for folder, cpu_name in CPU_SUBTITLES.items():
            if folder in filepath:
                subtitle = cpu_name
                break
        
        # Parse results
        df = parse_benchmark_results(filepath, list(parameters.keys()))
        
        # Generate all combinations of parameter values
        param_names = list(parameters.keys())
        param_value_lists = [list(parameters[name].keys()) for name in param_names]
        
        for param_values in product(*param_value_lists):
            # Build param_filters dict: {column: (value, display_name)}
            param_filters = {}
            for name, value in zip(param_names, param_values):
                param_filters[name] = (value, parameters[name][value])
            
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
