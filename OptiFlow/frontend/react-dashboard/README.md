# OptiFlow React Dashboard

Frontend for the OptiFlow Operations Dashboard.

## Planned Screens (M6-M7)

1. **Content Intelligence** — Modified/unchanged/added/deleted block visualization
2. **Localization Operations** — Job queue monitoring (queued/processing/failed)
3. **Translation Review** — Side-by-side original vs. translation comparison
4. **Cost & Performance** — Token usage, reuse rates, latency metrics

## Tech Stack

- React 18+ with TypeScript
- Vite build tool
- TanStack Query for data fetching
- Tailwind CSS for styling
- SignalR for real-time job status updates

## Setup

```bash
# Will be scaffolded in M6
npm create vite@latest . -- --template react-ts
npm install
npm run dev
```
