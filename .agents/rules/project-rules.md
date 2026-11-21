---
trigger: always_on
description: Mandatory rules for PriceWise LK - Sri Lankan Supermarket Price Comparison
---

# PriceWise LK - Strict Engineering and Project Rules

These rules are permanent and mandatory for all agents and developers working on PriceWise LK.

## 1. REAL APPLICATION DATA ONLY
- **NEVER fabricate, simulate, guess, or hardcode supermarket product data.**
- No fake products or sample supermarket prices.
- No mock API responses presented as real data.
- No randomly generated prices, discounts, inventory levels, ratings, images, or price histories.
- No hardcoded product cards containing fictional information in the UI or backend.
- No database seeders inserting fictional products.
- Never show a successful collection if a request failed.
- Never report a scraper as working without actually testing it against real endpoints.
- Never claim data is real-time if it is periodically collected.
- Synthetic fixtures are allowed ONLY inside isolated automated unit tests. They must NEVER enter production databases, runtime APIs, user-visible screens, or demonstration screenshots.
- If the database is empty, display a professionally designed empty state explaining that verified price information is not yet available.

## 2. RESPECT AUTHORIZATION AND ETHICAL ACCESS
- Only use access methods that are authorized, public, and permitted.
- Do not bypass authentication, CAPTCHA, access controls, rate limits, or anti-bot protections.
- Do not use stolen credentials, unauthorized session tokens, or undocumented private APIs without authorization.
- If automated collection is prohibited or cannot be verified as permitted (such as encrypted internal endpoints), mark the integration as **Blocked** and document the exact technical blocker and alternative (e.g., authorized data feed).
- Always respect source-specific rate limits and configure bounded retries.

## 3. SEPARATION OF CONCEPTS
Always distinguish between:
- Last attempted collection run timestamp
- Last successful observation timestamp
- Actual price change timestamp (if provided by source)
- Database insertion timestamp
Never mistake collection time for the time a supermarket changed its price.

## 4. PRODUCT MATCHING INTEGRITY
- Prioritize:
  1. Exact barcode / GTIN matches.
  2. Verified manufacturer product identifiers.
  3. Brand + normalized product name + quantity + unit matching.
  4. Carefully validated similarity matching.
- Never automatically treat two products as identical based only on similar names.
- Different sizes, variants, packaging types, and quantities must NEVER be merged as identical.
- Uncertain matches must be flagged for review (`verification_status: pending_review`) instead of appearing as verified comparisons.

## 5. SECURITY & CODE HYGIENE
- Never commit credentials, connection strings, session cookies, or secret service-role keys.
- Frontend must only use public anonymous read keys with Row Level Security (RLS).
- Privileged database writes must be restricted to trusted collector processes.
- All code must be modular, strongly typed, and covered by automated tests.
