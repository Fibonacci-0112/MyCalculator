---
applyTo: "PaycheckCalc.App/**/*.cs,PaycheckCalc.App/**/*.xaml"
---

# MAUI app instructions

- Follow MVVM. Pages should stay thin, and business logic belongs in `PaycheckCalc.Core` or mappers, not in XAML code-behind.
- Use CommunityToolkit.Mvvm source generators for observable properties and commands.
- Preserve the mapper boundary: `CalculatorViewModel` builds `StateInputValues`, maps to `PaycheckInput`, and maps domain results to `ResultCardModel`.
- Keep the state section schema-driven. Do not hardcode per-state controls in XAML if a schema-driven field can express the requirement.
- Use existing helper types such as `PickerItem<T>`, `EnumDisplay`, `StateFieldViewModel`, and `DecimalFormatBehavior` instead of duplicating patterns.
- Keep UI naming and structure consistent with the current folders and page layout.
- Do not add calculation math to converters, drawables, or page code-behind.
- If a UI change depends on new state inputs, update the state calculator schema and field resolution flow, not just the visual layer.
