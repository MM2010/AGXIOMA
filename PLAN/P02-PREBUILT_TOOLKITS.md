# P02 — Prebuilt Toolkits (150+)

> **Priority**: 🔴 P0 | **Dipende da**: P11 (Tools Infra) | **Usato da**: P08 (Runtime)
> **Target**: 150+ toolkit prebuilt (Agno ha ~120)

---

## Struttura

```
AGXIOME.Core/Tools/Toolkits/
├── Base/                    ← IToolkit, abstract ToolkitBase
│   ├── ToolkitBase.cs       ← Init, schema generation, validation
│   ├── ToolkitAttribute.cs  ← [Toolkit(Name, Description)]
│   └── ToolkitResult.cs     
├── Communication/           ← 15 toolkit
├── Productivity/            ← 18 toolkit
├── Data_Analysis/           ← 14 toolkit
├── Development/             ← 20 toolkit
├── AI_ML/                   ← 12 toolkit
├── Finance/                 ← 10 toolkit
├── Cloud_Infra/             ← 12 toolkit
├── Web_Social/              ← 18 toolkit
├── Search_Knowledge/        ← 12 toolkit
├── File_Storage/            ← 10 toolkit
├── Gaming_Creative/         ← 8 toolkit
└── Enterprise/              ← 12 toolkit
```

**Totale categorie: 12 | Toolkit: ~161**

---

## 🔴 Wave 1 — Communication (15)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-01 | **Slack** | send_message, list_channels, create_channel, add_reaction, upload_file | Slack API |
| T-02 | **Discord** | send_message, get_messages, create_thread, add_reaction, ban_user | Discord API |
| T-03 | **Telegram** | send_message, send_photo, send_document, get_updates, pin_message | Telegram Bot API |
| T-04 | **WhatsApp** | send_message, send_media, create_group, get_contacts | WhatsApp Cloud API |
| T-05 | **Email (SMTP)** | send_email, send_bulk, read_inbox, search_emails | SMTP/IMAP |
| T-06 | **Gmail** | send, search, label, trash, draft, thread | Gmail API |
| T-07 | **Microsoft Teams** | send_message, create_channel, schedule_meeting, list_members | Graph API |
| T-08 | **Zoom** | create_meeting, list_meetings, get_participants, record | Zoom API |
| T-09 | **Google Chat** | send_message, list_spaces, create_space, add_member | Google Chat API |
| T-10 | **Twilio SMS** | send_sms, send_whatsapp, list_messages, lookup | Twilio API |
| T-11 | **SendGrid** | send_email, send_template, list_lists, add_contact | SendGrid API |
| T-12 | **Mailchimp** | add_subscriber, send_campaign, list_audiences, create_template | Mailchimp API |
| T-13 | **Pushbullet** | push_note, push_link, push_file, list_devices | Pushbullet API |
| T-14 | **Webhook** | send_webhook, sign_payload, verify_webhook | HTTP |
| T-15 | **MQTT** | publish, subscribe, handle_message, list_topics | MQTT Broker |

---

## 🔴 Wave 2 — Productivity (18)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-16 | **Google Calendar** | list_events, create_event, update, delete, find_free_slots | Google Calendar API |
| T-17 | **Google Drive** | list_files, upload, download, search, share, create_folder | Google Drive API |
| T-18 | **Google Docs** | create_doc, read_doc, append, replace, export | Google Docs API |
| T-19 | **Google Sheets** | read_range, write_cell, append_row, create_sheet, format | Google Sheets API |
| T-20 | **Microsoft OneDrive** | list, upload, download, delete, share | Graph API |
| T-21 | **Notion** | search, create_page, update, query_database, append_block | Notion API |
| T-22 | **Confluence** | get_page, create_page, update, search, list_spaces | Confluence API |
| T-23 | **Jira** | create_issue, search_issues, update, add_comment, transition | Jira API |
| T-24 | **Linear** | create_issue, list_issues, update, search, get_team | Linear API |
| T-25 | **Asana** | create_task, update, list_projects, add_comment | Asana API |
| T-26 | **Trello** | create_card, move_card, list_boards, add_checklist | Trello API |
| T-27 | **Todoist** | create_task, list_projects, complete, update | Todoist API |
| T-28 | **Google Tasks** | list_tasks, create, update, complete, list_lists | Google Tasks API |
| T-29 | **Evernote** | create_note, search, list_notebooks, append | Evernote API |
| T-30 | **Obsidian** | create_note, search, append, get_vault | Obsidian Local |
| T-31 | **Habitica** | create_task, score, list_tasks, get_user | Habitica API |
| T-32 | **Calendly** | list_events, create_scheduling, get_user, cancel | Calendly API |
| T-33 | **Docusign** | send_envelope, get_status, list_templates, void | Docusign API |

---

## 🟡 Wave 3 — Data Analysis (14)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-34 | **SQL Database** | execute_query, list_tables, get_schema, run_transaction | ADO.NET |
| T-35 | **DuckDB** | query, load_csv, load_parquet, export, analyze | DuckDB .NET |
| T-36 | **PostgreSQL** | query, list_tables, vacuum, analyze, backup | Npgsql |
| T-37 | **MySQL** | query, list_tables, optimize, backup | MySqlConnector |
| T-38 | **SQLite** | query, attach, backup, vacuum, import_csv | Microsoft.Data.Sqlite |
| T-39 | **ClickHouse** | query, insert_batch, list_tables, optimize | ClickHouse.Client |
| T-40 | **Apache Spark** | run_sql, read_df, transform, write, show | .NET for Spark |
| T-41 | **Pandas-Like** | read_csv, filter, group, join, aggregate, plot | DataFrame |
| T-42 | **Excel** | read, write, format, chart, pivot, merge | ClosedXML/EPPlus |
| T-43 | **CSV** | read, write, merge, filter, validate, convert | CsvHelper |
| T-44 | **Parquet** | read, write, schema, merge, compress | Parquet.NET |
| T-45 | **JSON** | parse, transform, validate, query, flatten | System.Text.Json |
| T-46 | **XML** | parse, query, transform, validate, schema | System.Xml.Linq |
| T-47 | **GeoPandas-Like** | read_shapefile, spatial_join, buffer, plot, convert | NetTopologySuite |

---

## 🟡 Wave 4 — Development (20)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-48 | **GitHub** | create_pr, review, list_issues, create_issue, search_code, list_repos | Octokit |
| T-49 | **GitLab** | create_mr, list_projects, run_pipeline, get_commits | GitLab API |
| T-50 | **Bitbucket** | create_pr, list_repos, get_commits, manage_pipelines | Bitbucket API |
| T-51 | **Git** | clone, commit, push, pull, branch, log, diff | LibGit2Sharp |
| T-52 | **NuGet** | search_packages, get_metadata, list_versions, download | NuGet API |
| T-53 | **Docker** | build, pull, push, run, stop, ps, logs, exec | Docker.DotNet |
| T-54 | **Kubernetes** | get_pods, apply, delete, logs, exec, describe | k8s SDK |
| T-55 | **Terraform** | init, plan, apply, destroy, output, state | Terraform Cloud API |
| T-56 | **Pulumi** | up, preview, destroy, stack, config | Pulumi SDK |
| T-57 | **Helm** | install, upgrade, list, uninstall, repo | Helm SDK |
| T-58 | **AWS CLI** | s3, ec2, lambda, ssm, cloudwatch, iam | AWS SDK |
| T-59 | **Azure CLI** | vm, webapp, functions, storage, sql | Azure SDK |
| T-60 | **GCP CLI** | storage, run, functions, compute, sql | Google.Cloud |
| T-61 | **System Info** | cpu, memory, disk, process, network, os | System.Diagnostics |
| T-62 | **File System** | read, write, copy, move, delete, search, zip | System.IO |
| T-63 | **Shell Command** | exec, run_script, pipe, timeout, env | System.Diagnostics |
| T-64 | **Python Runner** | exec_python, pip_install, eval, script | Pythonnet/Process |
| T-65 | **Jupyter** | list_kernels, execute, get_output, create_notebook | Notebook API |
| T-66 | **Build Tools** | dotnet_build, npm_build, msbuild, cmake | Process |
| T-67 | **Code Quality** | analyze, lint, format, complexity, coverage | Roslyn/Sonar |

---

## 🟡 Wave 5 — AI/ML (12)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-68 | **OpenAI Vision** | analyze_image, describe, ocr, object_detect | OpenAI API |
| T-69 | **DALL-E** | generate_image, edit, vary | OpenAI API |
| T-70 | **Stable Diffusion** | txt2img, img2img, upscale, inpaint | SD API |
| T-71 | **Whisper** | transcribe, translate, diarize | Whisper API |
| T-72 | **ElevenLabs TTS** | text_to_speech, voice_clone, list_voices | ElevenLabs API |
| T-73 | **HuggingFace Hub** | search_model, load, infer, list | HF API |
| T-74 | **Scikit-Learn** | train, predict, evaluate, tune, pipeline | ML.NET |
| T-75 | **PyTorch** | run_inference, load_model, transform | TorchSharp |
| T-76 | **ONNX Runtime** | run, load, optimize, quantize | ONNX Runtime |
| T-77 | **LangChain Bridge** | run_chain, invoke_tool, call_llm | LangChain |
| T-78 | **Semantic Kernel** | run_skill, invoke_function, memory | SK SDK |
| T-79 | **ML.NET AutoML** | train_model, evaluate, save, predict | ML.NET |

---

## 🟡 Wave 6 — Finance (10)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-80 | **Yahoo Finance** | get_quote, get_history, get_financials, search | Yahoo API |
| T-81 | **Alpha Vantage** | get_quote, fx, crypto, sector, technical | Alpha Vantage |
| T-82 | **Polygon.io** | get_aggs, trades, news, financials, options | Polygon API |
| T-83 | **CoinGecko** | get_price, list_coins, market_data, trend | CoinGecko API |
| T-84 | **Binance** | get_price, place_order, get_balance, klines | Binance API |
| T-85 | **Stripe** | create_payment, refund, list_customers, invoice | Stripe SDK |
| T-86 | **Plaid** | get_accounts, get_transactions, auth, identity | Plaid .NET |
| T-87 | **QuickBooks** | create_invoice, list_customers, get_report | QB API |
| T-88 | **Xero** | create_invoice, list_contacts, get_balance | Xero API |
| T-89 | **Forex** | get_rate, convert, list_pairs, historical | Forex API |

---

## 🟡 Wave 7 — Cloud & Infra (12)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-90 | **AWS S3** | list, upload, download, delete, presign, copy | AWS SDK |
| T-91 | **AWS Lambda** | invoke, list, create, update, logs | AWS SDK |
| T-92 | **AWS DynamoDB** | query, put, get, scan, delete, create_table | AWS SDK |
| T-93 | **AWS EC2** | list, start, stop, create, describe, ssh | AWS SDK |
| T-94 | **Azure Blob** | list, upload, download, delete, sas | Azure.Storage |
| T-95 | **Azure Functions** | invoke, list, create, logs | Azure SDK |
| T-96 | **GCP Storage** | list, upload, download, delete, sign | Google.Cloud |
| T-97 | **GCP Cloud Run** | deploy, list, logs, manage_revisions | Google.Cloud |
| T-98 | **Cloudflare** | purge_cache, list_zones, dns, workers | Cloudflare API |
| T-99 | **Vercel** | deploy, list_projects, get_logs, env | Vercel API |
| T-100 | **Netlify** | deploy, list_sites, functions, forms | Netlify API |
| T-101 | **DigitalOcean** | list_droplets, create, snapshot, resize | DO API |

---

## 🟢 Wave 8 — Web & Social (18)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-102 | **Twitter/X** | post_tweet, search, get_timeline, follow, dm | Twitter API |
| T-103 | **Reddit** | get_posts, search, comment, submit, dm | Reddit API |
| T-104 | **LinkedIn** | share_post, search_people, send_message | LinkedIn API |
| T-105 | **Instagram** | post, get_feed, search, comment, story | IG API |
| T-106 | **YouTube** | search, get_video, list_comments, upload | YouTube API |
| T-107 | **TikTok** | get_video, search, post, analytics | TikTok API |
| T-108 | **Medium** | create_post, publish, get_stats, list | Medium API |
| T-109 | **Dev.to** | create_post, list_articles, comment | DEV API |
| T-110 | **Hashnode** | create_post, publish, get_stats | Hashnode API |
| T-111 | **WordPress** | create_post, list_pages, upload_media, comment | WP API |
| T-112 | **Shopify** | list_products, create_order, get_customers | Shopify API |
| T-113 | **WooCommerce** | list_products, create_order, get_reports | Woo API |
| T-114 | **Squarespace** | list_products, create_order, get_analytics | SS API |
| T-115 | **Zapier** | execute_zap, list_zaps, check_status | Zapier API |
| T-116 | **IFTTT** | trigger_event, list_services | IFTTT API |
| T-117 | **Make (Integromat)** | execute_scenario, list_scenarios | Make API |
| T-118 | **Web Scraper** | fetch, parse, extract, screenshot, crawl | HtmlAgilityPack |
| T-119 | **RSS** | fetch, parse, search, subscribe | Syndication |

---

## 🟢 Wave 9 — Search & Knowledge (12)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-120 | **Web Search (Google)** | search, get_snippets, suggest, images | Google API |
| T-121 | **Web Search (Bing)** | search, news, images, video, entities | Bing API |
| T-122 | **Web Search (SearXNG)** | search, aggregate, get_categories | SearXNG API |
| T-123 | **Tavily** | search, extract, get_context, news | Tavily API |
| T-124 | **Exa** | search, get_contents, similar, find_links | Exa API |
| T-125 | **Brave Search** | search, news, image, video, summarize | Brave API |
| T-126 | **SerpAPI** | search, images, news, shopping, maps | SerpAPI |
| T-127 | **Serper** | search, images, news, places, shopping | Serper API |
| T-128 | **Firecrawl** | scrape, crawl, map, extract, search | Firecrawl API |
| T-129 | **Wikipedia** | search, get_summary, get_page, list_categories | Wiki API |
| T-130 | **Arxiv** | search_papers, get_paper, list_new, download | Arxiv API |
| T-131 | **PubMed** | search, get_article, get_abstract, mesh | PubMed API |

---

## 🟢 Wave 10 — File & Storage (10)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-132 | **Local File** | read, write, delete, move, copy, search, watch | System.IO |
| T-133 | **FTP/SFTP** | list, upload, download, delete, move | SSH.NET |
| T-134 | **Google Cloud Storage** | upload, download, list, delete, sign | Google.Cloud |
| T-135 | **MinIO** | upload, download, list, presign | Minio SDK |
| T-136 | **Dropbox** | upload, download, list, search, share | Dropbox API |
| T-137 | **Box** | upload, download, list, search, lock | Box API |
| T-138 | **SharePoint** | upload, download, list, search, checkin | Graph API |
| T-139 | **Compression** | zip, unzip, tar, gzip, 7z, brotli | System.IO.Compression |
| T-140 | **Image Processing** | resize, crop, rotate, convert, filter, ocr | SkiaSharp |
| T-141 | **PDF Processing** | read, extract, merge, split, watermark, sign | iText/PdfPig |

---

## 🟢 Wave 11 — Gaming & Creative (8)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-142 | **Steam** | get_games, get_player, get_achievements | Steam API |
| T-143 | **Twitch** | get_streams, search, follow, clip, chat | Twitch API |
| T-144 | **Spotify** | search, play, playlist, recommend, podcast | Spotify API |
| T-145 | **Last.fm** | get_artist, get_album, scrobble, recommend | Last.fm API |
| T-146 | **Unsplash** | search, get_random, download, list | Unsplash API |
| T-147 | **Pexels** | search_video, search_image, curated, popular | Pexels API |
| T-148 | **Giphy** | search, trending, translate, stickers | Giphy API |
| T-149 | **OpenAI Image Gen** | generate, edit, vary, analyze | OpenAI API |

---

## 🟢 Wave 12 — Enterprise (12)

| # | Toolkit | Funzioni | Provider |
|---|---------|----------|----------|
| T-150 | **Salesforce** | query, create, update, search, describe | Salesforce API |
| T-151 | **HubSpot** | search, create_contact, deal, ticket | HubSpot API |
| T-152 | **Zendesk** | create_ticket, search, get_views, macro | Zendesk API |
| T-153 | **ServiceNow** | create_incident, search, update, workflow | ServiceNow API |
| T-154 | **Datadog** | query_metrics, monitors, logs, events | Datadog SDK |
| T-155 | **PagerDuty** | trigger_incident, list, acknowledge, resolve | PD API |
| T-156 | **Sentry** | get_issues, search, list_projects, release | Sentry SDK |
| T-157 | **Grafana** | query, list_dashboards, annotate, alert | Grafana API |
| T-158 | **New Relic** | query, list_apps, get_alerts, dashboard | NR API |
| T-159 | **Elasticsearch** | search, index, delete, aggregate, bulk | Elastic.Clients |
| T-160 | **Okta** | list_users, create, get_groups, sessions | Okta SDK |
| T-161 | **Auth0** | list_users, get_roles, create_user, logs | Auth0 SDK |

---

## Test per Toolkit

```csharp
// Ogni toolkit ha:
[Fact] public async Task ToolkitName_HasCorrectTools()       // verifica signature
[Fact] public async Task ToolkitName_Init_ValidConfig()       // init con config valida
[Fact] public async Task ToolkitName_ToolX_Works()            // test funzionale singolo tool
[Fact] public async Task ToolkitName_HttpError_Handled()      // errore HTTP gestito
[Fact] public async Task ToolkitName_EmptyConfig_Fails()      // config vuota fallisce
```

**Totale toolkit: 161 | File: ~200 | Test: ~800**
