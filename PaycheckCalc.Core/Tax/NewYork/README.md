# NewYork (NY)

NewYork state income tax withholding is currently computed by the generic
annualized percentage-method engine (`PercentageMethodWithholdingAdapter`
+ `PercentageMethodStateTaxCalculator`) using the `UsState.NY` entry in
`PaycheckCalc.Core/Tax/State/StateTaxConfigs2026.cs`.

This folder exists as a placeholder so that any future NewYork-specific
logic that cannot be expressed through `PercentageMethodConfig` (for
example, table-driven withholding, unique allowances, or a bespoke
`IStateWithholdingCalculator` implementation) can be added here alongside
the other state modules under `PaycheckCalc.Core/Tax/`.
