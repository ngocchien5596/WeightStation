# Settings Page Overrides

> **PROJECT:** Weighbridge ERP
> **Generated:** 2026-04-26 12:00:23
> **Page Type:** Dashboard / Data View

> ⚠️ **IMPORTANT:** Rules in this file **override** the Master file (`design-system/MASTER.md`).
> Only deviations from the Master are documented here. For all other rules, refer to the Master.

---

## Page-Specific Rules

### Layout Overrides

- **Max Width:** 1400px or full-width
- **Grid:** 12-column grid for data flexibility
- **Form Controls:** Compact Inline alignment (Label-left, TextBox-right) to shrink height footprint.
- **Sections:** 1. Hero (Video/Mission), 2. Solutions by Industry, 3. Solutions by Role, 4. Client Logos, 5. Contact Sales

### Spacing Overrides

- **Content Density:** High — optimize for information display

### Typography Overrides

- No overrides — use Master typography

### Color Overrides

- **Strategy:** Corporate: Navy/Grey. High integrity. Conservative accents.

### Component Overrides

- Avoid: Use arbitrary large z-index values
- Avoid: Single row actions only
- Avoid: Auto-play high-res video loops

---

## Page-Specific Components

- No unique components for this page

---

## Recommendations

- Effects: display: grid, grid-template-columns: repeat(12 1fr), gap: 1rem, mathematical ratios, clear hierarchy
- Layout: Define z-index scale system (10 20 30 50)
- Data Entry: Allow multi-select and bulk edit
- Sustainability: Click-to-play or pause when off-screen
- CTA Placement: Contact Sales (Primary) + Login (Secondary)
