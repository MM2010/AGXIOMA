# AGXIOME Exception Model

## Current status

AGXIOME already had these foundation pieces:

- `AgxiomeErrorCode`
- `ErrorCatalog`
- `AgxiomeException`

The remaining gap was operational, not conceptual:

- many boundaries still threw raw framework exceptions
- logging did not prefer AGXIOME structured error codes when available
- reports could not distinguish safe user messaging from technical diagnostics consistently

## Rules

1. User-facing payloads expose `UserMessage`, stable string code, and numeric code only.
2. Technical diagnostics stay in `TechnicalMessage`, `Details`, `InnerException`, and logger output.
3. Stack traces are log-only diagnostics and must never be part of user-facing strings.
4. Provider boundaries should translate vendor exceptions into AGXIOME exceptions as early as possible.

## Implemented in this pass

1. Added numeric error identity via `AgxiomeException.NumericCode`.
2. Added `AgxiomeStorageException` for storage/vector-store boundaries.
3. Added storage-specific structured codes:
   - `DB_InvalidCollectionName`
   - `DB_InvalidRecordIdentifier`
   - `DB_ProviderOperationFailed`
   - `DB_CollectionMissing`
4. Updated `AgxiomeLogger` to emit AGXIOME error codes and safe user/technical separation when logging `AgxiomeException` instances.
5. Updated hot vector-store boundaries to throw structured AGXIOME storage exceptions instead of opaque provider failures.

## Immediate adoption targets

Apply the same translation pattern to:

1. Remaining vector stores with raw `ArgumentException` and `InvalidOperationException` throws.
2. Toolkit configuration guards currently throwing `InvalidOperationException` for missing secrets/settings.
3. Runtime/API middleware so HTTP responses always return safe error envelopes with AGXIOME codes.

## Reporting guidance

Every surfaced error should be representable as:

```json
{
  "code": "ERR-0411",
  "numericCode": 411,
  "category": "Storage",
  "message": "L'operazione sul data store non e' riuscita.",
  "retryable": true
}
```

The stack trace remains exclusively in logs and traces, never in the response envelope.