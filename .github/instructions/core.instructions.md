---
applyTo: "PaycheckCalc.Core/**/*.cs"
---

# Core library instructions

- `PaycheckCalc.Core` must remain UI-agnostic. Do not add MAUI, XAML, or view-model dependencies here.
- Preserve sealed/init-only/value-oriented model patterns where they already exist.
- Calculation code must use `decimal` and remain explicit about annualization, deductions, taxable wages, and rounding.
- Keep `PayCalculator` as the orchestrator, not the place for state-specific branching.
- Prefer extending `IStateWithholdingCalculator`, `PercentageMethodConfig`, or JSON-backed calculators over ad hoc conditionals scattered across the core.
- Keep comments strong around legal or table-driven tax rules.
- When editing a state calculator, inspect its matching tests and update them if behavior changes.
- If code and docs disagree, trust the implementation and tests first, then repair docs separately.
