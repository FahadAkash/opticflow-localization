# OptiFlow API Documentation

This directory will contain OpenAPI / Swagger specifications and endpoint documentation.

## Endpoints (from design doc)

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/v1/cms/connections` | Create CMS connection |
| GET | `/api/v1/cms/connections` | List connections |
| POST | `/api/v1/content/sync` | Trigger content sync |
| GET | `/api/v1/content/{id}/versions` | List content versions |
| GET | `/api/v1/content/{id}/diff` | Compare versions |
| POST | `/api/v1/localization/jobs` | Create localization job (202) |
| GET | `/api/v1/localization/jobs/{id}` | Job status |
| GET | `/api/v1/localization/jobs/{id}/blocks` | Block-level results |
| GET | `/api/v1/reviews` | Pending reviews |
| POST | `/api/v1/reviews/{id}/approve` | Approve review |
| POST | `/api/v1/reviews/{id}/reject` | Reject review |
| GET | `/api/v1/analytics/costs` | Cost analytics |
| GET | `/api/v1/analytics/performance` | Performance metrics |
| POST | `/api/v1/webhooks/optimizely` | Receive CMS events |
