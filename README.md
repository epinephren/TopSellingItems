# TopSellingItems

Dalamud API 14 plugin that ranks items by estimated sales/day using Universalis sale history.

## Notes

- This version avoids the Universalis aggregated endpoint and instead uses the documented sale history route.
- Sales/day is computed locally from recent sale entries over the configured history window.
- The default scan limit is intentionally capped to keep scans responsive.
- `Min Listing / Floor` falls back to the cheapest recent sale if live listing data is unavailable in the history response.
