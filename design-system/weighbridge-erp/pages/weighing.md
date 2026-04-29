# Weighing Page Design System

This file overrides `MASTER.md` for the Weighing page.

---

## Grid Styling

### Overweight Records (Bản ghi quá tải)

| Property | Rule |
|----------|------|
| **Text Color** | `Brushes.Red` / `#FF0000` |
| **Background** | `Brushes.Transparent` / No background highlight |
| **Font Weight** | `FontWeights.Bold` (keep existing bold for emphasis) |

**Reasoning:** The user requested to simplify the visual indicator for overweight records to reduce visual noise while maintaining the warning through text color.

## Layout & Spacing

### Main Content Split

| Area | Rule |
|------|------|
| **Form Area (Left)** | Flexible width (`*`) |
| **Weighing Panel (Right)** | Fixed width: `520px` (Increased from `480px`) |

**Reasoning:** Larger weighing panel provides better visibility for the real-time weight display and avoids crowding of controls in full-screen mode.

### Header Controls

| Control | Style Rule |
|---------|------------|
| **Search Labels** | Bold, White, with consistent spacing (`Margin: 16,0,8,0`) |
| **Refresh Button** | Vibrant orange (`#F97316`) or primary blue (`#3498DB`), bold text, clear icon |

**Reasoning:** Header controls need to be highly legible and the "Refresh" action should be prominent as it's a frequent operational task.

## KPI & Stats Styling

### KPI Text Colors

To maintain visual consistency between the summary statistics and the data grid, the following color rules apply to the KPI row:

| KPI | Color | Reason |
|-----|-------|--------|
| **Xuất bộ / Xuất thủy** | `Green` / `#008000` | Matches `OUTBOUND` record foreground |
| **Nhập** | `Black` / `#000000` | Matches `INBOUND` (default) record foreground |

---

## Components

### DataGrid

- **Frozen Columns**: The first 3 columns (**SỐ PC**, **MÃ ĐKPT**, **SỐ PTVC**) are frozen to maintain context during horizontal scrolling.
- **Row Highlight:** Only apply default selection and hover highlights.
- **Status Indicators:** Use text color only for status-based highlighting (e.g., Green for Outbound, Red for Overweight).
