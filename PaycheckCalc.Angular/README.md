# PaycheckCalc.Angular

Angular 19 single-page front end for PaycheckCalc. Talks to
[`PaycheckCalc.Api`](../PaycheckCalc.Api) (ASP.NET Core Web API) which
delegates all payroll calculations to `PaycheckCalc.Core` — the same engine
used by the MAUI and Blazor heads. This project intentionally contains **no**
tax logic of its own.

## Prerequisites

- Node.js 20+ and npm
- The `PaycheckCalc.Api` backend running (see the top-level [README](../README.md))

## Scripts

```bash
npm install       # install dependencies
npm start         # run the dev server at http://localhost:4200
npm run build     # production build to ./dist
```

## API base URL

The API base URL is configured in
[`src/app/paycheck-api.service.ts`](src/app/paycheck-api.service.ts) and
defaults to `http://localhost:5200` (the API's development profile). To point
at a different backend, edit the `baseUrl` constant or replace it with an
Angular environment-driven value.

## Structure

```
src/app/
├── app.component.{ts,html,css}  # Single paycheck calculator page
├── app.config.ts                # Root providers (HttpClient, zone change detection)
├── paycheck-api.service.ts      # Typed HttpClient wrapper for PaycheckCalc.Api
└── paycheck.models.ts           # TypeScript types mirroring the API DTOs
```
