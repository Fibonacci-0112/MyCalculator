# PaycheckCalc.Web

A **Blazor WebAssembly** front-end for [PaycheckCalc](https://fibonacci-0112.github.io/MyCalculator/)
that runs entirely in the browser — no server required. It references `PaycheckCalc.Core` directly
so all payroll calculations use the same tax engine as the MAUI app.

## Live Demo

**[https://fibonacci-0112.github.io/MyCalculator/](https://fibonacci-0112.github.io/MyCalculator/)**

Deployed automatically to GitHub Pages on every push to `main` via
`.github/workflows/deploy-web.yml`.

## Local Development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9) (stable, pinned via `global.json` in this folder)
- Blazor WASM workload:

```bash
cd PaycheckCalc.Web
dotnet workload install wasm-tools
```

### Run

```bash
dotnet run --project PaycheckCalc.Web
```

Then open `http://localhost:5000` in your browser.

> **Note:** When running locally, the `<base href="/MyCalculator/">` in `index.html` is adjusted
> automatically by the Blazor dev server.  You do **not** need to change it for local runs.

### Publish

```bash
dotnet publish PaycheckCalc.Web/PaycheckCalc.Web.csproj -c Release -o publish
```

The output is in `publish/wwwroot/` and contains a standalone `_framework/` folder that
can be served by any static host.

## Architecture Notes

### Target Framework

This project targets **net9.0** (stable) rather than the repository-wide .NET 11 preview because:

- Blazor WASM packages for .NET 11 preview are not yet consistently available on GitHub-hosted runners.
- `PaycheckCalc.Core` is multi-targeted (`net11.0;net9.0`) with QuestPDF/PDF export excluded from
  the net9.0 build (no native WASM assets). All other Core functionality is fully available.

### Tax JSON Loading

`PaycheckCalc.Core/Data/*.json` tax tables are linked into `wwwroot/data/` via `<Content>` items
in the `.csproj`. `Program.cs` fetches them with a temporary `HttpClient` **before** building the DI
container so every calculator is fully initialized at startup with no lazy loading.

### IPaycheckRepository — localStorage

`Storage/LocalStoragePaycheckRepository.cs` implements `IPaycheckRepository` using
[Blazored.LocalStorage](https://github.com/Blazored/LocalStorage). Saved paychecks are serialized to
JSON and stored under the key `paycheckcalc_saved_paychecks` in browser localStorage.

### GitHub Pages — `<base href>` and SPA Routing

GitHub Pages serves this project at `https://fibonacci-0112.github.io/MyCalculator/`.
Two things are required for client-side routing to work:

1. `wwwroot/index.html` has `<base href="/MyCalculator/" />`.
2. `wwwroot/404.html` contains a redirect script that encodes the deep-link path into a query string
   and sends the browser back to `index.html`, which then navigates to the correct Blazor route.

The `.nojekyll` file in `wwwroot/` prevents Jekyll from stripping the `_framework/` folder during
GitHub Pages deployment.
