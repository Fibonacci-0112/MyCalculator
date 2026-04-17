# Self-Employment Module

PaycheckCalc includes a self-employment / contractor tax estimation module that computes Schedule C net profit, self-employment (SE) tax, QBI deduction, federal income tax, and state income tax.

---

## Overview

The SE module is a peer to the W-2 paycheck calculator. It has its own orchestrator (`SelfEmploymentCalculator`), input model (`SelfEmploymentInput`), result model (`SelfEmploymentResult`), and dedicated UI pages.

### Core Components

| Component | Location | Responsibility |
|---|---|---|
| `SelfEmploymentCalculator` | `Core/Tax/SelfEmployment/` | Orchestrates the full SE tax estimation pipeline |
| `SelfEmploymentTaxCalculator` | `Core/Tax/SelfEmployment/` | Computes Schedule SE (SS + Medicare + Additional Medicare) |
| `QbiDeductionCalculator` | `Core/Tax/SelfEmployment/` | Computes Section 199A QBI deduction (Form 8995 / 8995-A) |
| `SelfEmploymentInput` | `Core/Models/` | Domain input: Schedule C, filing status, state, QBI, estimated payments |
| `SelfEmploymentResult` | `Core/Models/` | Domain result: full tax breakdown and quarterly estimates |

### UI Components

| Component | Location |
|---|---|
| `SelfEmploymentPage` | `App/Views/` — Input form with 3 tabs |
| `SelfEmploymentResultsPage` | `App/Views/` — Results display with 2 tabs |
| `SelfEmploymentViewModel` | `App/ViewModels/` — Singleton VM owning input state and results |

---

## Calculation Pipeline

`SelfEmploymentCalculator.Calculate(SelfEmploymentInput)` runs these steps:

### Step 1: Schedule C Net Profit

```
Net Profit = Gross Revenue − Cost of Goods Sold − Total Business Expenses
```

### Step 2: Self-Employment Tax (Schedule SE)

`SelfEmploymentTaxCalculator` computes:

1. **SE taxable earnings** = Net Profit × 92.35% (the self-employment equivalent of the employer/employee split).
2. **Social Security tax** = 12.4% of SE taxable earnings, capped at the remaining room under the $184,500 annual wage base (coordinated with any W-2 Social Security wages).
3. **Medicare tax** = 2.9% of SE taxable earnings (no cap).
4. **Additional Medicare tax** = 0.9% on combined wages + SE earnings above $200,000 (coordinated with W-2 Medicare wages).
5. **Total SE tax** = Social Security + Medicare + Additional Medicare.
6. **Deductible half** = 50% of total SE tax (above-the-line deduction on Form 1040).

#### W-2 FICA Coordination

For taxpayers who also have W-2 employment, the calculator accepts optional `W2SocialSecurityWages` and `W2MedicareWages` parameters:

- W-2 SS wages reduce the remaining SS wage base, so SE Social Security tax is only assessed on the gap.
- W-2 Medicare wages reduce the Additional Medicare threshold, so the 0.9% surtax correctly applies to combined earnings.

### Step 3: Adjusted Gross Income

```
AGI = Other Income + max(0, Net Profit) − Deductible Half of SE Tax
```

### Step 4: Standard Deduction

2026 projected standard deduction amounts:

| Filing Status | Amount |
|---|---|
| Single / Married Filing Separately | $15,700 |
| Married Filing Jointly | $31,400 |
| Head of Household | $23,550 |

If itemized deductions exceed the standard deduction, the excess is added.

### Step 5: QBI Deduction (Section 199A)

`QbiDeductionCalculator` computes the Qualified Business Income deduction:

- Generally 20% of qualified business income, limited to the lesser of the QBI component or 20% of taxable income before QBI.
- Subject to W-2 wage/UBIA limitation and SSTB phase-out rules above income thresholds.
- Inputs: qualified business income, taxable income before QBI, filing status, SSTB flag, W-2 wages, UBIA of qualified property.

### Step 6: Taxable Income

```
Taxable Income = max(0, AGI − Deductions − QBI Deduction)
```

### Step 7: Federal Income Tax

Uses the same `Irs15TPercentageCalculator` as the W-2 module, passing taxable income with annual frequency and a Step 2-checked W-4 for single-earner accuracy.

### Step 8: State Income Tax

Reuses the `StateCalculatorRegistry` by passing net SE income as annual gross wages through the state withholding engine. The same state calculators and dynamic state inputs work for both W-2 and SE scenarios.

### Step 9: Summary

| Field | Formula |
|---|---|
| Total Federal Tax | Federal Income Tax + Total SE Tax |
| Total Tax | Total Federal Tax + State Income Tax |
| Effective Tax Rate | Total Tax / (Gross Revenue + Other Income) × 100 |
| Estimated Quarterly Payment | Total Tax / 4 |
| Over/Under Payment | Estimated Tax Payments Already Made − Total Tax |

---

## Navigation

The SE module is accessible via the app's flyout menu:

- **Self-Employment** — Input page with tabs for Schedule C, federal info, and QBI details.
- **SE Results** — Results page with tabs for tax breakdown and quarterly estimates.
