# Agxioma Support Hub

Support hub minimale che espone il flusso backoffice usato dal frontend `backoffice-demo-ui`.

## LLM attivo

Il provider configurato di default e' `ollama` con modello `qwen2.5-coder:1.5b`.

Per default il backend punta a `http://localhost:11434`. In Docker Compose usa `http://host.docker.internal:11434`, quindi l'istanza Ollama deve essere attiva sulla macchina host e avere il modello caricato.

## Endpoint principali

- `POST /backoffice`
- `GET /backoffice/{runId}`
- `PATCH /backoffice/{runId}`
- `POST /backoffice/confirm`
- `POST /backoffice/survey`
- `GET /backoffice/dashboard`
- `GET /backoffice/runs`
- `GET /backoffice/tickets`
- `GET /backoffice/surveys`

## Avvio locale

```bash
dotnet run --project AGXIOMA/apps/agxioma-support-hub/AgxiomaSupportHub.csproj
```

Per default il backend ascolta su `http://localhost:5012` e gira in modalita' `local` con SQLite come store canonico e read model operativo.

Per cambiare modello/provider in locale puoi sovrascrivere:

```bash
Agent__Provider=ollama Agent__Model=qwen2.5-coder:1.5b Agent__Endpoint=http://localhost:11434 dotnet run --project AGXIOMA/apps/agxioma-support-hub/AgxiomaSupportHub.csproj
```

## Modalita' enterprise

La modalita' `enterprise` usa:

- PostgreSQL come canonical store;
- Redis come hot store;
- SQLite locale come projection store per dashboard e listing.

### Avvio con Docker Compose

1. Copia `AGXIOMA/apps/agxioma-support-hub/.env.enterprise.example` in `.env` nella cartella `AGXIOMA` oppure esporta le stesse variabili nel tuo ambiente.
2. Avvia lo stack:

```bash
docker compose -f AGXIOMA/docker-compose.support-hub-enterprise.yml up --build -d
```

Le variabili LLM principali per lo stack sono:

- `AGENT_PROVIDER` default `ollama`
- `AGENT_MODEL` default `qwen2.5-coder:1.5b`
- `AGENT_ENDPOINT` default `http://host.docker.internal:11434`

3. Verifica il dashboard:

```bash
curl http://localhost:5012/backoffice/dashboard
```

## Canali reali

Se lasci i valori vuoti, il runtime registra le notifiche in `outbox` e la control room mostra i canali come non configurati.

Per attivare i canali reali configura una o piu' di queste sezioni:

- `Backoffice__Email__*`
- `Backoffice__Telegram__BotToken`
- `Backoffice__WhatsApp__AccessToken`
- `Backoffice__WhatsApp__PhoneNumberId`