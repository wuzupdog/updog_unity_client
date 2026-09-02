# Changelog

## 0.3.1

- Add the Unity `.meta` files required for Git-installed packages to import the runtime and test assemblies.
- Declare the built-in Unity Web Request module used by the HTTP error reporter.

## 0.3.0

- Add bounded custom metric reporting through a local Updog host agent's StatsD listener.
- Allow metric-only Unity processes to run without possessing the Updog ingestion key.
- Add environment defaults for service, release, and the local StatsD endpoint.

## 0.2.0

- Bulk error delivery through a bounded in-memory queue with count and byte limits.
- Stable event/request IDs, occurrence timestamps, and shared resource metadata.
- Retry classification, `Retry-After`, full-jitter backoff, 413 splitting, delivery counters, and bounded flush/shutdown APIs.
