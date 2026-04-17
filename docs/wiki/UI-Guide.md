# UI Guide

PaycheckCalc uses .NET MAUI with a flyout navigation pattern. This page describes the app's pages, input forms, and features.

---

## Navigation

The app uses **Shell Flyout** navigation with six main sections:

| Flyout Item | Route | Page |
|---|---|---|
| Inputs | `//Inputs` | `InputsPage` — Multi-tab input form |
| Results | `//Results` | `ResultsPage` — Per-period and annual results |
| Compare | `//Compare` | `ComparePage` — Side-by-side comparison |
| Saved Paychecks | `//Saved` | `SavedPaychecksPage` — Manage saved entries |
| Self-Employment | `//SelfEmployment` | `SelfEmploymentPage` — SE tax input form |
| SE Results | `//SEResults` | `SelfEmploymentResultsPage` — SE tax results |

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

- **Filing Status** — Picker: Single or Married.
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

The Compare page enables side-by-side comparison of two calculations:

1. On the Results page, tap **Save to Compare** to snapshot the current calculation.
2. Change inputs and recalculate.
3. Navigate to the Compare page to see both scenarios side-by-side with differences highlighted.

---

## Saved Paychecks Page

Manage previously saved paycheck calculations:

- **List View** — Shows all saved entries with name and timestamp.
- **Load** — Restores saved inputs back into the calculator (via `PaycheckInputRestorer`).
- **Rename** — Update the display name of a saved entry.
- **Delete** — Remove a saved entry.

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

## Input Formatting

The app uses `DecimalFormatBehavior` to format numeric inputs consistently. Enum values use `EnumDisplay` helpers for user-friendly picker labels (e.g., `Biweekly` → "Bi-Weekly").

---

## Theming

The app uses a blue-themed color scheme:
- Primary: `#1565C0` (toolbar, flyout selected)
- Flyout background: `#F0F4FF`
- Tab bar: White text on blue background
