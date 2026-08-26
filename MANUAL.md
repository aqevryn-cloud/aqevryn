# Aqevryn — User Manual

> **Version:** 0.1.0 (C#/.NET 8)
> **Purpose:** Autonomous Technology Research & Publishing Agent

---

## Table of Contents

1. [Overview](#1-overview)
2. [Quick Start](#2-quick-start)
3. [Installation & Setup](#3-installation--setup)
4. [Configuration](#4-configuration)
5. [Source Configuration](#5-source-configuration)
6. [CLI Commands](#6-cli-commands)
7. [Pipeline Walkthrough](#7-pipeline-walkthrough)
8. [Docker Deployment](#8-docker-deployment)
9. [Scheduling](#9-scheduling)
10. [API Server](#10-api-server)
11. [Website & Publishing](#11-website--publishing)
12. [Database Backups](#12-database-backups)
13. [Troubleshooting](#13-troubleshooting)
14. [Architecture Overview](#14-architecture-overview)

---

## 1. Overview

Aqevryn is an autonomous technology research system that:

1. **Collects** content from technology sources (RSS, arXiv, GitHub, Hacker News, Reddit)
2. **Discovers** trending technology topics
3. **Analyzes** trends, researchability, and market viability
4. **Ranks** topics by significance
5. **Researches** the selected topic in depth
6. **Generates** an original research article
7. **Reviews** the article for quality
8. **Publishes** via GitHub Pull Request for human approval

The system runs on a 4 GB RAM VPS using Docker, and publishes to GitHub Pages.

---

## 2. Quick Start

### Development (no Docker, no API keys needed)

```bash
# Clone the repository
cd Aqevryn-csharp

# Build
dotnet build

# Run the pipeline in dry-run mode (no external sources, no publishing)
dotnet run --project src/Aqevryn -- --dry-run run

# Check health
dotnet run --project src/Aqevryn -- health
```

### Production (Docker, minimal setup)

```bash
# 1. Configure
cp .env.example .env
# Edit .env — set LLM_API_KEY and GITHUB_TOKEN

# 2. Start
docker compose up -d

# 3. Run pipeline
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll --dry-run run"

# 4. When ready for real publishing
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll run"
```

---

## 3. Installation & Setup

### Prerequisites

- **.NET 8 SDK** (for development)
- **Docker & Docker Compose** (for production)
- **Git** (for version control)
- **PostgreSQL 16+** (optional, for development without Docker)

### Development Setup

```bash
# 1. Clone
git clone <repo-url> Aqevryn-csharp
cd Aqevryn-csharp

# 2. Build
dotnet build

# 3. Test
dotnet test

# 4. Run (dry-run)
dotnet run --project src/Aqevryn -- --dry-run run
```

### Production Setup (Ubuntu VPS)

```bash
# 1. Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# 2. Clone Aqevryn
git clone <repo-url> Aqevryn-csharp
cd Aqevryn-csharp

# 3. Configure
cp .env.example .env
nano .env   # Fill in your API keys

# 4. Start
docker compose up -d

# 5. Verify
docker compose ps
docker compose logs -f aqevryn
```

---

## 4. Configuration

### Environment Variables (`.env`)

```ini
# --- Application ---
APP_ENV=development              # development, production, test
LOG_LEVEL=INFO                   # DEBUG, INFO, WARNING, ERROR

# --- LLM Provider ---
LLM_PROVIDER=openai              # openai, anthropic, or mock
LLM_API_KEY=sk-...               # Your API key (REQUIRED for real use)
LLM_MODEL=gpt-4o                 # Model name
LLM_MAX_TOKENS=4096              # Max tokens per request
LLM_TEMPERATURE=0.3              # Creativity (0.0 = deterministic, 1.0 = creative)

# --- GitHub ---
GITHUB_TOKEN=ghp_...             # GitHub personal access token
GITHUB_OWNER=your-username       # GitHub username or org
GITHUB_REPOSITORY=your-repo      # Repository name
GITHUB_DEFAULT_BRANCH=main       # Default branch

# --- Database ---
DATABASE_URL=Host=postgres;Database=aqevryn;Username=aqevryn;Password=aqevryn

# --- Pipeline ---
AUTO_PUBLISH=false               # true = auto-publish, false = draft PRs
MIN_PUBLICATION_SCORE=90         # Minimum editorial score for publication
DRY_RUN=false                    # true = skip all publishing

# --- Scoring Weights (must sum to 1.0) ---
WEIGHT_TREND=0.25
WEIGHT_RESEARCHABILITY=0.25
WEIGHT_MARKET=0.20
WEIGHT_NOVELTY=0.15
WEIGHT_TECHNICAL_SIGNIFICANCE=0.15

# --- Scheduler Intervals ---
COLLECT_INTERVAL_HOURS=6         # Source collection frequency
ANALYZE_INTERVAL_HOURS=12        # Trend analysis frequency
RANK_INTERVAL_HOURS=24           # Full research frequency

# --- Cost Controls ---
MAX_SOURCES_PER_RESEARCH=30
MAX_RETRIES=3
REQUEST_TIMEOUT_SECONDS=60
```

### Scoring Weights

The final topic score is calculated as:

```
Final Score = Trend × 0.25 + Researchability × 0.25 + Market × 0.20 + Novelty × 0.15 + Technical Significance × 0.15
```

You can adjust these weights in `.env` to prioritize different aspects.

---

## 5. Source Configuration

Sources are configured in `sources.yaml`. Copy the example and customize:

```bash
cp sources.example.yaml sources.yaml
```

### Source Types

| Type | Description | API Key Needed? | Example |
|------|-------------|-----------------|---------|
| `rss` | RSS/Atom feeds | No | Technology blogs, news sites |
| `arxiv` | arXiv research papers | No | cs.AI, cs.LG, cs.CL |
| `github` | GitHub repositories | Yes (`GITHUB_TOKEN`) | Trending repos by topic |
| `hackernews` | Hacker News stories | No | Top/new/best stories |
| `reddit` | Reddit posts | No | Subreddit hot/new/top |

### Example `sources.yaml`

```yaml
sources:
  - name: Hacker News
    type: hackernews
    feed_type: topstories
    max_items: 30
    enabled: true
    category: technology

  - name: arXiv AI
    type: arxiv
    categories:
      - cs.AI
      - cs.LG
      - cs.CL
    max_results: 30
    enabled: true
    category: artificial_intelligence

  - name: GitHub Trending AI
    type: github
    topics:
      - ai
      - machine-learning
      - llm
      - agents
    min_stars: 100
    enabled: true
    category: developer_tools

  - name: Reddit r/MachineLearning
    type: reddit
    subreddits:
      - MachineLearning
      - artificial
    feed_type: hot
    limit: 25
    enabled: true
    category: artificial_intelligence

  - name: MIT Technology Review
    type: rss
    url: https://www.technologyreview.com/feed/
    enabled: true
    category: technology
```

### Common RSS Feeds to Add

```yaml
  - name: Google AI Blog
    type: rss
    url: https://blog.research.google/feed.xml
    enabled: true
    category: artificial_intelligence

  - name: OpenAI Blog
    type: rss
    url: https://openai.com/blog/feed.xml
    enabled: true
    category: artificial_intelligence

  - name: Netflix TechBlog
    type: rss
    url: https://netflixtechblog.com/feed
    enabled: true
    category: engineering

  - name: Reddit r/programming
    type: reddit
    subreddits:
      - programming
    feed_type: hot
    limit: 15
    enabled: true
    category: developer_tools
```

---

## 6. CLI Commands

### Command Reference

| Command | Description | Options |
|---------|-------------|---------|
| `run` | Run the full pipeline (discover → analyze → research → write → review → publish) | `--dry-run` |
| `discover` | Collect sources and discover topics | `--dry-run` |
| `analyze` | Analyze trends, researchability, market viability | `--dry-run` |
| `research` | Conduct deep research | `--dry-run` |
| `write` | Generate a research article | `--dry-run` |
| `review` | Run editorial review | `--dry-run` |
| `publish` | Publish via GitHub PR | `--dry-run` |
| `scheduler` | Run the scheduler loop (continuous) | — |
| `api` | Start the HTTP API server | — |
| `health` | Check application health | — |
| `build-site` | Build the static research website | — |

### Global Options

| Option | Description |
|--------|-------------|
| `--dry-run` | Run without publishing anything |
| `-v` or `--verbose` | Enable debug logging |
| `--help` | Show help |

### Usage Examples

```bash
# Full pipeline (dry-run)
dotnet run --project src/Aqevryn -- --dry-run run

# Full pipeline (real — will publish)
dotnet run --project src/Aqevryn -- run

# Just collect sources
dotnet run --project src/Aqevryn -- --dry-run discover

# Research a specific topic
dotnet run --project src/Aqevryn -- --dry-run research

# Check health
dotnet run --project src/Aqevryn -- health

# Build website
dotnet run --project src/Aqevryn -- build-site

# Start scheduler (runs until Ctrl+C)
dotnet run --project src/Aqevryn -- scheduler
```

### Docker Equivalents

```bash
# Run pipeline
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll --dry-run run"

# Check health
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll health"

# View logs
docker compose logs -f aqevryn
```

---

## 7. Pipeline Walkthrough

### What happens when you run `aqevryn run`

```
[1/9] Collecting technology sources...
  → RSS, arXiv, GitHub, HN, Reddit adapters fetch content
  → Content is normalized (HTML→text, whitespace cleaned)
  → Duplicates are removed (by URL and content hash)
  → Output: 184 new articles

[2/9] Extracting topics...
  → Keyword matching across 16 technology categories
  → Each article is classified into categories (AI, security, cloud, etc.)
  → Topics with < 2 articles are discarded
  → Output: 73 candidate topics

[3/9] Clustering...
  → Related topics are grouped into 14 canonical categories
  → "AI Agents" + "Agentic AI" + "Autonomous AI" → "AI Agents"
  → Output: 41 unique topic clusters

[4/9] Trend analysis...
  → 5 signals measured: mention count, growth, sources, recency, diversity
  → Each signal is normalized to 0-100
  → Weighted composite score calculated
  → Output: Top topic "AI Coding Agents" — Trend Score: 91

[5/9] Researchability...
  → Source quality, content depth, and topic specificity scored
  → Checks for primary sources, academic papers, implementations
  → Output: Researchability Score: 94

[6/9] Market viability...
  → 50+ known companies checked against article content
  → Industry adoption, enterprise activity, developer activity scored
  → Output: Market Viability Score: 88

[7/9] Researching...
  → Research plan created (question, objectives, subquestions)
  → Sources classified by reliability (research papers > docs > blogs)
  → Findings extracted, contradictions detected
  → Output: 27 sources analyzed, 8 primary sources

[8/9] Generating article...
  → Article structured with introduction, analysis, findings, etc.
  → Multi-source synthesis (not paraphrasing a single source)
  → References and methodology sections included
  → Output: Article generated

[9/9] Editorial review...
  → 6 dimensions scored: factual, research, originality, technical, writing, SEO
  → Critical issues checked (missing references, missing metadata)
  → Publish recommendation: APPROVE / REJECT / REVISE
  → Output: Editorial Score: 93 — Status: APPROVED

→ Creating Markdown with YAML front matter
→ Creating GitHub branch: research/ai-coding-agents
→ Creating Pull Request with topic details and scores
→ Pipeline completed successfully.
```

### Quality Gates

An article cannot proceed if:

| Gate | Condition | Result |
|------|-----------|--------|
| **Citations** | No references | REJECT |
| **Sources** | Sources cannot be verified | REJECT |
| **Claims** | Major claims unsupported | REJECT |
| **Research Question** | Not answered in article | REVISE |
| **Originality** | Excessive similarity to source | REJECT |
| **Editorial Score** | Below threshold (default 70) | REVISE |
| **Metadata** | Missing slug, title, or description | REJECT |

---

## 8. Docker Deployment

### Starting the System

```bash
# Start all services
docker compose up -d

# Check status
docker compose ps
docker compose logs -f aqevryn
```

### Running the Pipeline

```bash
# Dry run (safe — no publishing)
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll --dry-run run"

# Real run (will publish if approved)
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll run"
```

### Other Commands

```bash
# Health check
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll health"

# Build website
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll build-site"

# Restart
docker compose restart aqevryn

# View logs
docker compose logs -f aqevryn

# Stop
docker compose down
```

### Updating Aqevryn

```bash
# 1. Pull latest code
git pull

# 2. Rebuild and restart
docker compose up -d --build --force-recreate aqevryn
```

---

## 9. Scheduling

The scheduler runs the pipeline on configurable intervals. By default, the container runs the scheduler.

### Default Schedule

| Task | Interval | Description |
|------|----------|-------------|
| Source collection | Every 6 hours | Lightweight — fetches from all sources |
| Trend analysis | Every 12 hours | Includes collection + analysis |
| Full research | Every 24 hours | Complete pipeline end-to-end |

### Configure in `.env`

```ini
COLLECT_INTERVAL_HOURS=6
ANALYZE_INTERVAL_HOURS=12
RANK_INTERVAL_HOURS=24
```

### How Scheduler Works

- The scheduler runs an infinite loop, checking every 60 seconds if it's time for each task
- Only one research project runs at a time (4 GB RAM constraint)
- If a task fails, it's logged and the scheduler continues
- Graceful shutdown on Ctrl+C or SIGTERM

### Manual Scheduler Start

```bash
# Development
dotnet run --project src/Aqevryn -- scheduler

# Docker (already running by default)
# The container runs the scheduler automatically
```

---

## 10. API Server

A lightweight HTTP API server provides health monitoring endpoints.

### Starting

```bash
# Development
dotnet run --project src/Aqevryn -- api

# Docker
docker exec -d aqevryn sh -c "cd /app && dotnet Aqevryn.dll api"
```

### Endpoints

#### `GET /health`

```json
{
  "status": "ok",
  "version": "0.1.0",
  "uptime_seconds": 3600,
  "timestamp": "2026-08-26T12:00:00Z"
}
```

#### `GET /status`

```json
{
  "application": {
    "name": "Aqevryn",
    "version": "0.1.0",
    "uptime_seconds": 3600
  },
  "pipeline": {
    "auto_publish": false,
    "min_publication_score": 90
  },
  "llm": {
    "provider": "openai"
  }
}
```

---

## 11. Website & Publishing

### How Publishing Works

```
Aqevryn → Research → Markdown → GitHub branch → Pull Request → Human Approval → Merge → GitHub Pages
```

### Publishing Flow

1. **Article is generated and reviewed** — if editorial score ≥ threshold and no critical issues
2. **Markdown is created** — with YAML front matter (title, date, categories, tags, scores)
3. **GitHub branch is created** — `research/<topic-slug>`
4. **Article is committed** — to `articles/<filename>.md`
5. **Pull Request is opened** — with topic details and scores in the description
6. **Human reviews** — merges the PR to approve
7. **GitHub Actions deploy** — the website updates automatically

### PR Content Example

```
Title: Research: What Makes AI Coding Agents Reliable?

Body:
## Topic
AI Coding Agents

## Scores
| Trend Score | 91 |
| Researchability | 94 |
| Market Viability | 88 |
| Final Score | 92 |
| Editorial Score | 93 |

_Generated by Aqevryn_
```

### Building the Website Locally

```bash
# Generate static site
dotnet run --project src/Aqevryn -- build-site

# Output: website/ directory with HTML files
#  - index.html — Home page with latest articles
#  - about.html — About page
#  - methodology.html — Research methodology
#  - articles/<slug>.html — Individual article pages
#  - style.css — Responsive stylesheet
```

### GitHub Pages Deployment

The `.github/workflows/deploy.yml` workflow automatically deploys to GitHub Pages when articles are merged to `main`:

1. Trigger: Push to `main` that changes `articles/published/*`
2. Build: Copies website files to `_site/`
3. Deploy: GitHub Pages publishes the site

---

## 12. Database Backups

### Backup Script

```bash
# Create a backup
./scripts/backup_database.sh

# Backup to a specific directory
./scripts/backup_database.sh /path/to/backups

# Output: backups/aqevryn_20260826_120000.dump.gz
```

### Restore Script

```bash
# Restore from a backup
./scripts/restore_database.sh backups/aqevryn_20260826_120000.dump.gz
```

### Automatic Cleanup

Backups older than 30 days are automatically deleted (configurable via `RETENTION_DAYS`).

### Best Practices

- Store backups off-VPS (e.g., S3, rsync, or external storage)
- Test restoration periodically
- The database contains: collected articles, topics, research findings, generated articles, editorial reviews, publication records

---

## 13. Troubleshooting

### Common Issues

| Symptom | Cause | Solution |
|---------|-------|----------|
| `Could not read existing file "*.cache"` | Stale build cache | `rm -rf src/Aqevryn/obj && dotnet build` |
| Container exits immediately | No command specified | Use `command: ["scheduler"]` in docker-compose.yml |
| `Framework 'Microsoft.AspNetCore.App' not found` | Wrong base image | Use `dotnet/aspnet:8.0` instead of `dotnet/runtime:8.0` |
| No sources found | Missing `sources.yaml` | Copy `sources.example.yaml` to `sources.yaml` |
| Pipeline fails at "research" | No LLM API key | Set `LLM_API_KEY` in `.env` |
| Pipeline fails at "publish" | No GitHub token | Set `GITHUB_TOKEN`, `GITHUB_OWNER`, `GITHUB_REPOSITORY` |
| Topic rejected | Low researchability | Add more primary sources (arXiv papers, official docs) |

### Logs

```bash
# View all logs
docker compose logs -f aqevryn

# View last 100 lines
docker compose logs --tail=100 aqevryn

# Follow logs for a specific command
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll --dry-run run" 2>&1
```

### Health Check

```bash
# Quick health check
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll health"

# Expected output:
# {
#   "status": "ok",
#   "version": "0.1.0",
#   "environment": "development"
# }
```

### Resetting

```bash
# Stop everything
docker compose down

# Remove volumes (WARNING: deletes all data)
docker compose down -v

# Start fresh
docker compose up -d
```

---

## 14. Architecture Overview

```
                    INTERNET
                        │
                        ▼
            ┌───────────────────────┐
            │     DOCKER COMPOSE     │
            │   ┌───────────────┐   │
            │   │   Aqevryn     │   │
            │   │  (Pipeline)   │   │
            │   └───────┬───────┘   │
            │           │           │
            │   ┌───────┴───────┐   │
            │   │  PostgreSQL   │   │
            │   │  (pgvector)   │   │
            │   └───────────────┘   │
            └───────┬───────────────┘
                    │
    ┌───────────────┼───────────────────┐
    │               │                   │
    ▼               ▼                   ▼
  LLM APIs     GitHub API          RSS/APIs
  (OpenAI)     (PRs, Pages)        (Sources)
    │               │                   │
    └───────────────┼───────────────────┘
                    │
                    ▼
           ┌────────────────┐
           │  GitHub Pages  │
           │  (Website)     │
           └────────────────┘
```

### Directory Structure

```
Aqevryn-csharp/
├── src/Aqevryn/
│   ├── Agents/           # 10 research agents
│   ├── Api/              # HTTP API server
│   ├── Common/           # Shared models
│   ├── Config/           # Configuration
│   ├── Database/         # EF Core models
│   ├── Orchestration/    # Pipeline orchestrator
│   ├── Publishing/       # Markdown, GitHub, Site
│   ├── Research/         # Research reports
│   ├── Scheduler/        # Scheduled tasks
│   └── Sources/          # 5 source adapters
├── tests/                # 48 xUnit tests
├── scripts/              # Backup/restore
├── Dockerfile
├── docker-compose.yml
├── soul.md
└── .env.example
```

### Data Flow

```
Sources → SourceItem → Normalizer → Deduplicator → Articles
                                                          ↓
Articles → TopicDiscovery → TopicClustering → TrendAnalysis
                                                          ↓
Researchability → MarketViability → TopicRanker → SelectedTopic
                                                          ↓
ResearchPlanner → ResearchAgent → EvidenceValidator → ResearchReport
                                                          ↓
ArticleWriter → EditorialReview → Markdown → GitHub PR → Website
```

---

*This manual covers Aqevryn v0.1.0 (C#/.NET 8). For the Python version, see the `/home/admin1/Aqevryn` directory.*