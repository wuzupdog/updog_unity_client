# Changelog

## 0.2.0

- Bulk error delivery through a bounded in-memory queue with count and byte limits.
- Stable event/request IDs, occurrence timestamps, and shared resource metadata.
- Retry classification, `Retry-After`, full-jitter backoff, 413 splitting, delivery counters, and bounded flush/shutdown APIs.
