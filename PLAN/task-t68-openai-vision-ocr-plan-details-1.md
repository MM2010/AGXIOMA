---
goal: AGXIOME T-68 OpenAI Vision PLAN_DETAILS
version: 1.0
date_created: 2026-05-16
last_updated: 2026-05-16
owner: AGXIOME implementation track
status: Planned
tags: [task, planning, ai-ml, vision, ocr, openai, t-68]
---

# Introduction

Questo documento definisce il `PLAN_DETAILS` ultra-dettagliato del task singolo `T-68 OpenAI Vision`, con focus esplicito su `analyze_image`, `describe`, `ocr` e `object_detect`. Nel repo esiste gia' una prima implementazione di `OpenAiVisionToolkit`, ma oggi il task e' solo parzialmente coperto: la toolkit usa un path generico `chat/completions`, non espone ancora un contratto robusto action-by-action, e i test esistenti sono ancora leggeri o indiretti.

## 1. Surface Reale Attuale

- `AGXIOME.Toolkits/AiMl/AiMlToolkits.cs`
- `AGXIOME.Core/Utils/ImageUtil.cs`
- `AGXIOME.Tests/F12/F12AiMlTests.cs`
- `AGXIOME.Tests/VisionProviderTests.cs`

## 2. Gap Attuali

- `GAP-001`: `OpenAiVisionToolkit` non distingue ancora in modo forte le azioni `analyze_image`, `describe`, `ocr`, `object_detect`.
- `GAP-002`: non esiste ancora una validazione completa degli input `image_url` vs `image_base64`.
- `GAP-003`: non esiste un mapping strutturato dei risultati OCR o object detection.
- `GAP-004`: i test F12 sono di stato base e di errore generico, non di copertura funzionale vera.
- `GAP-005`: manca una strategia chiara di live tests gated per OpenAI reale.
- `GAP-006`: manca una politica esplicita di sanitizzazione di URL, base64 e errori provider.

## 3. Obiettivi Del Task

1. chiudere `OpenAiVisionToolkit` come toolkit realmente usabile e non solo come wrapper minimale
2. definire il contratto di input/output per le quattro azioni core
3. garantire copertura funzionale, offensiva, aggressiva e osservabilita'
4. usare provider reale quando possibile, con gating esplicito via `OPENAI_API_KEY`

## 4. Contract Target Per Azione

### A. `analyze_image`

- Scopo: analisi generale multimodale dell'immagine.
- Input richiesti: `image_url` oppure `image_base64`.
- Input opzionali: `prompt`, `model`, `max_tokens`, `detail`.
- Output atteso: testo descrittivo piu' payload raw provider opzionale.
- Failure attese: nessuna immagine fornita, URL invalido, base64 invalido, auth error, rate limit.

### B. `describe`

- Scopo: descrizione sintetica o dettagliata guidata da prompt predefinito o custom.
- Input richiesti: immagine.
- Input opzionali: `style`, `verbosity`, `prompt`, `model`.
- Output atteso: descrizione normalizzata adatta a consumo applicativo.
- Failure attese: stessa classe di `analyze_image`.

### C. `ocr`

- Scopo: estrazione testo leggibile da screenshot, documenti e immagini.
- Input richiesti: immagine.
- Input opzionali: `prompt`, `language_hint`, `return_blocks`, `model`.
- Output atteso: testo estratto, blocchi opzionali, metadata minimi.
- Failure attese: immagine vuota, testo non rilevabile, immagini troppo grandi, risposta provider ambigua.

### D. `object_detect`

- Scopo: rilevamento oggetti/entita' visive in immagine.
- Input richiesti: immagine.
- Input opzionali: `prompt`, `labels_hint`, `return_boxes`, `model`.
- Output atteso: lista oggetti rilevati e, se supportato dal prompt/parse, coordinate o regioni descritte.
- Failure attese: provider non restituisce struttura sufficiente, parse non robusto, immagine non valida.

## 5. File Target

- `AGXIOME.Toolkits/AiMl/AiMlToolkits.cs`
- `AGXIOME.Core/Utils/ImageUtil.cs`
- `AGXIOME.Tests/F12/F12AiMlTests.cs`
- `AGXIOME.Tests/VisionProviderTests.cs`

## 6. Detailed Work Breakdown

### TASK-001 - Hardening input validation

- verificare che venga accettato esattamente uno tra `image_url` e `image_base64`
- rifiutare input vuoti o doppi
- validare MIME/URL usando `ImageUtil`
- introdurre limiti chiari di dimensione per base64, se necessari

### TASK-002 - Action routing esplicito

- mappare esplicitamente `analyze_image`, `describe`, `ocr`, `object_detect`
- rifiutare azioni sconosciute con errore AGXIOME coerente
- evitare che tutte le azioni si riducano allo stesso prompt generico

### TASK-003 - Prompt strategy per azione

- definire prompt di default separati per analisi, descrizione, OCR e detection
- consentire override via parametro `prompt`
- evitare prompt vaghi che rendono non deterministico il parse del risultato

### TASK-004 - Output normalization

- normalizzare la risposta provider in `ToolkitResult`
- per `ocr`, restituire testo estratto e metadata minimi
- per `object_detect`, restituire lista oggetti e, se disponibile, region hints
- per `describe`, restituire testo compatto e privo di envelope inutile

### TASK-005 - Error mapping e sicurezza

- intercettare `401`, `403`, `429`, `5xx`
- convertire gli errori in surface AGXIOME coerente
- non esporre API key, prompt completi sensibili o base64 raw nei messaggi di errore

### TASK-006 - Tests locali forti

- espandere `F12AiMlTests` con test per azione, non solo test di `Id` e `Category`
- espandere `VisionProviderTests` per validazione utility e content building
- usare fake input reali strutturati, non chiamate remote, per la maggioranza dei test

### TASK-007 - Live tests gated

- aggiungere test `[Fact(Skip=...)]` o gating esplicito via env per OpenAI reale
- verificare almeno un caso `analyze_image` e uno `ocr` su input noto
- isolare questi test dal path standard CI se manca la configurazione

## 7. Test Matrix

### Functional tests

- `analyze_image` con `image_url` valido restituisce `ToolkitResult` coerente
- `describe` con `image_url` e prompt custom instrada il payload corretto
- `ocr` con immagine valida usa il prompt corretto per estrazione testo
- `object_detect` costruisce richiesta coerente con prompt di detection
- `image_base64` valido produce content part compatibile OpenAI

### Offensive tests

- nessuna immagine fornita -> errore esplicito
- entrambe `image_url` e `image_base64` -> errore esplicito
- `image_url` non immagine -> errore esplicito
- `image_base64` malformato -> errore esplicito
- `api_key` mancante -> `Skipped` o errore controllato secondo contratto toolkit
- azione sconosciuta -> `UnknownAction`
- risposta HTTP `401` -> errore autenticazione controllato
- risposta HTTP `429` -> errore rate limit controllato

### Aggressive tests

- chiamate concorrenti con immagini diverse non corrompono payload o stato
- payload grandi ma validi restano entro i limiti e falliscono in modo prevedibile se oltre soglia
- retry policy, se introdotta, non duplica richieste in modo incontrollato
- cancellazione via `CancellationToken` interrompe la richiesta senza lasciare stato sporco

### Observability tests

- log ed errori non contengono API key
- log ed errori non dumpano base64 integrale
- URL immagine mascherato o minimizzato quando riportato in diagnostica
- reason codes distinguibili per auth, validation, rate limit e provider failure

## 8. Real Provider Strategy

- Test locali principali: nessuna dipendenza da rete, focus su routing, validazione e shape del payload.
- Test live gated: OpenAI reale solo se `OPENAI_API_KEY` e test asset controllati sono disponibili.
- Mock policy: evitare mock dell'intera toolkit; consentire solo boundary stub dell'HTTP layer se serve per validare parse e mapping senza rete.

## 9. Acceptance Criteria

- `AC-001`: `OpenAiVisionToolkit` distingue chiaramente le quattro azioni principali.
- `AC-002`: input validation robusta per URL/base64.
- `AC-003`: error mapping coerente con AGXIOME.
- `AC-004`: test F12 e Vision coprono funzionale/offensivo/aggressivo/osservabilita'.
- `AC-005`: almeno un live test gated e documentato per OpenAI reale.

## 10. Risks & Assumptions

- `RISK-001`: il provider OpenAI vision non garantisce nativamente output strutturato per object detection se non guidato bene dal prompt.
- `RISK-002`: OCR via modello multimodale puo' essere meno deterministico di un motore OCR dedicato.
- `RISK-003`: l'uso di `HttpClient` statico richiede attenzione se si introduce configurazione per test o provider override.
- `ASSUMPTION-001`: il task non richiede bounding boxes native obbligatorie se il provider non le restituisce in modo affidabile.
- `ASSUMPTION-002`: `ImageUtil` resta il punto canonico per validazione e content building image-based.

## 11. Next Execution Order

1. hardening input validation
2. action routing esplicito
3. prompt strategy per azione
4. output normalization
5. error mapping sicuro
6. estensione test locali
7. live tests gated OpenAI
