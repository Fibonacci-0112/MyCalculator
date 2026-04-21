# UI Guide

PaycheckCalc ships with two front-ends. The MAUI app uses a flyout navigation pattern; the Blazor Server head exposes a streamlined two-page flow. This page describes the pages, input forms, and features.

---

## Navigation (MAUI)

The MAUI app uses **Shell Flyout** navigation. The flyout exposes the following items:

| Flyout Item | Route | Page |
|---|---|---|
| Inputs | `//Inputs` | `InputsPage` — multi-tab input form |
| Results | `//Results` | `ResultsPage` — per-period and annual results |
| Compare | `//Compare` | `ComparePage` — side-by-side comparison |
| Saved Paychecks | `//Saved` | `SavedPaychecksPage` — manage saved entries |
| Self-Employment | `//SelfEmployment` | `SelfEmploymentPage` — SE tax input form |
| SE Results | `//SEResults` | `SelfEmploymentResultsPage` — SE tax results |
| Annual Projection | `//AnnualProjection` | `AnnualProjectionPage` — Phase 8 annual inputs |
| Jobs & YTD | `//JobsAndYtd` | `JobsAndYtdPage` — W-2 jobs and year-to-date |
| Other Income & Adjustments | `//OtherIncomeAdjustments` | `OtherIncomeAdjustmentsPage` — Schedule 1 adjustments, other income |
| Credits | `//Credits` | `CreditsPage` — CTC, education, saver's credit inputs |
| Quarterly Estimates | `//QuarterlyEstimates` | `QuarterlyEstimatesPage` — Form 1040-ES |
| What-If | `//WhatIf` | `WhatIfPage` — withholding-suggestion and scenario analysis |
| Annual Results | `//AnnualResults` | `AnnualTaxResultsPage` — full Form 1040 estimate |

The Annual Projection / Jobs & YTD / Other Income / Credits / Quarterly Estimates / What-If pages all share the singleton `AnnualTaxSession` as their source of truth, and a final Form 1040 estimate is produced on the Annual Results page.

## Navigation (Blazor Server)

The Blazor head currently exposes two Razor pages backed by a scoped `CalculatorSessionState`:

- **Inputs** — captures `PaycheckInput` (pay & hours, federal W-4, state, and deductions).
- **Results** — renders the `PaycheckResult` for the most recently submitted inputs in the current circuit.

---

## Inputs Page

The Inputs page is a tabbed form with four sub-pages:

### Pay & Hours Tab (`PayHoursPage`)

- **Pay Frequency** — Picker: Weekly, Biweekly, Semimonthly, Monthly, Quarterly, Semiannual, Annual, Daily.
- **Hourly Rate** — Decimal input.
- **Regular Hours** — Decimal input (per pay period).
- **Overtime Hours** — Decimal input (per pay period).
- **Overtime Multiplier** — Decimal input (defaults to 1.5).
- **YTD Social Security Wages** — For annual cap tracking.
- **YTD Medicare Wages** — For Additional Medicare threshold tracking.
- **Paycheck Number** — 1-based integer for annual projection.

### Federal W-4 Tab (`FederalPage`)

- **Filing Status** — Picker: Single / Married Filing Separately, Married Filing Jointly, or Head of Household (see `FederalFilingStatus`).
- **Step 2 Checkbox** — Toggle (two jobs / spouse works).
- **Step 3 Credits** — Decimal (child/dependent tax credits).
- **Step 4(a) Other Income** — Decimal.
- **Step 4(b) Deductions** — Decimal.
- **Step 4(c) Extra Withholding** — Decimal.

### State Tab (`StatePage`)

- **State** — Picker populated from `StateCalculatorRegistry.SupportedStates` (all 50 + DC).
- **Dynamic Fields** — Rendered from the selected state's `GetInputSchema()`. Fields update automatically when the state selection changes.

Common dynamic fields include:
- Filing Status (picker with state-specific options)
- Allowances / Exemptions (integer)
- Dependents (integer, for states like Alabama)
- Extra Withholding (decimal)

### Deductions Tab (`DeductionsPage`)

- **Add Deduction** — Creates a new deduction entry.
- Each deduction has:
  - **Name** — Free-text label.
  - **Amount** — Dollar or percentage value.
  - **Amount Type** — Picker: Dollar or Percentage.
  - **Type** — Picker: Pre-Tax or Post-Tax.
  - **Reduces State Taxable Wages** — Toggle (for pre-tax deductions).

---

## Results Page

The Results page has two sub-tabs:

### Period Tab

Displays the per-paycheck breakdown:

- Gross Pay
- Pre-Tax Deductions
- Federal Taxable Income
- Federal Withholding
- State (name), State Taxable Wages, State Withholding
- State Disability Insurance (when applicable, with dynamic label)
- Social Security
- Medicare
- Additional Medicare
- Local Withholding + Local Head Tax (when a locality is selected)
- Post-Tax Deductions
- **Net Pay**

Additional features:
- **Doughnut Chart** — Visual breakdown of gross pay by category (federal tax, state tax, SS, Medicare, net pay).
- **Save to Compare** — Snapshots the current calculation for side-by-side comparison.
- **Save as New Paycheck** — Persists the calculation to local storage with a custom name.
- **Export CSV** — Generates a CSV file and opens the OS share sheet.
- **Export PDF** — Generates a PDF report via QuestPDF and opens the OS share sheet.

### Annual Tab

Displays annualized projections:

- Total annual gross, taxes, and net pay.
- Current paycheck number and remaining pay periods.
- Projected year-end over/under withholding estimate.

---

## Compare Page

The Compare page supports two layouts:

- **Saved-vs-current (default)** — Tap **Save to Compare** on the Results page to snapshot the current calculation, change inputs and recalculate, then open Compare to see both scenarios side-by-side with differences highlighted.
- **Multi-scenario** — When one or more entries are pushed from the Saved Paychecks page into the singleton `ComparisonSession`, Compare renders all of them side-by-side instead of the legacy 1-vs-1 view.

---

## Saved Paychecks Page

Manage previously saved paycheck calculations:

- **List View** — Shows all saved entries with name and timestamp.
- **Load** — Restores saved inputs back into the calculator (via `PaycheckInputRestorer`).
- **Rename** — Update the display name of a saved entry.
- **Delete** — Remove a saved entry.
- **Compare** — Push one or more selected entries into `ComparisonSession` for the multi-scenario Compare layout.

When a saved paycheck is loaded, re-saving from the Results page overwrites the existing entry (tracked via `LoadedPaycheckId`).

---

## Self-Employment Pages

### Self-Employment Input Page

Three-tab input form:
- **Schedule C** — Gross revenue, COGS, business expenses.
- **Federal** — Filing status, other income, W-2 FICA wages, estimated tax payments.
- **QBI** — SSTB flag, W-2 wages, UBIA of qualified property.

### SE Results Page

Two-tab results display:
- **Tax Breakdown** — Full itemized breakdown from Schedule C through net tax.
- **Quarterly Estimates** — Suggested quarterly payment and over/under analysis.

---

## Annual (Phase 8) Pages

The annual Form 1040 flow is split across several flyout pages, all sharing a singleton `AnnualTaxSession` that backs the `AnnualTaxInputMapper` → `TaxYearProfile` conversion:

- **Annual Projection** — High-level filing status, anticipated annual wages, and withholdings.
- **Jobs & YTD** — One or more W-2 jobs (optionally per-spouse via `W2JobInput.Holder` for MFJ) and year-to-date Social Security / Medicare wages.
- **Other Income & Adjustments** — Schedule 1 income lines and adjustments.
- **Credits** — Child Tax Credit, education credits (Form 8863), saver's credit (Form 8880), and other credit inputs.
- **Quarterly Estimates** — Form 1040-ES quarterly estimate calculator.
- **What-If** — Scenario exploration including the withholding-suggestion calculator that rounds per-period suggestions to cents.
- **Annual Results** — Year-end Form 1040 estimate combining withholding, credits, NIIT, and balance due / refund.

Saved annual scenarios are persisted through `IAnnualScenarioRepository` → `JsonAnnualScenarioRepository` (`saved_annual_scenarios.json`).

---

## Input Formatting

The app uses `DecimalFormatBehavior` to format numeric inputs consistently. Enum values use `EnumDisplay` helpers for user-friendly picker labels (e.g., `Biweekly` → "Bi-Weekly").

---

## Theming

The app uses a blue-themed color scheme:
- Primary: `#1565C0` (toolbar, flyout selected)
- Flyout background: `#F0F4FF`
- Tab bar: White text on blue background
