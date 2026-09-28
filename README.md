# Aqevryn

> **Autonomous Research & Intelligence Organization**
> 
> _Discover important questions, investigate evidence, develop knowledge, and communicate rigorous research across disciplines._

---

## Overview

Aqevryn is an autonomous research agent that discovers trends, analyzes topics, and produces evidence-based research papers across **multiple disciplines** — technology, science, economics, finance, business, history, geopolitics, climate, energy, education, law, culture, society, and more.

It operates as a fully autonomous pipeline:

```
Sources → Collect → Discover Topics → Analyze → Rank → Research → Write → Review → Publish
```

And engages with the broader AI agent community on [Moltbook](https://www.moltbook.com/u/aqevryn) — commenting on discussions, requesting peer review, and collaborating with other agents.

---

## Philosophy

Aqevryn is **not a content generator**. It is a **research organization implemented as software**.

Its purpose is not to maximize the number of articles produced. Its purpose is to:

- Discover meaningful questions
- Investigate evidence
- Challenge assumptions
- Compare competing explanations
- Identify knowledge gaps
- Synthesize information
- Develop defensible conclusions
- Communicate those conclusions clearly

One excellent research article is worth more than ten shallow AI-generated articles.

> **Full philosophy:** See [`soul.md`](soul.md) — the agent's core identity, principles, and behavioral guidelines (52 sections).

---

## Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET 8 SDK (for development)
- A GitHub personal access token (for publishing)
- An OpenRouter API key (optional, for LLM-enhanced research)

### Development

```bash
# Clone and build
git clone <repo-url>
cd Aqevryn-csharp
dotnet build

# Run the pipeline (dry-run — no publishing)
dotnet run --project src/Aqevryn -- --dry-run run

# Check health
dotnet run --project src/Aqevryn -- health
```

### Production (Docker)

```bash
# 1. Configure
cp .env.example .env
nano .env   # Set GITHUB_TOKEN, LLM_API_KEY

# 2. Configure sources
cp sources.example.yaml sources.yaml

# 3. Start
docker compose up -d --build

# 4. Run pipeline
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll run"

# 5. View dashboard
# Open http://localhost:9888 in your browser
```

---

## Configuration

### Environment Variables (`.env`)

See [`.env.example`](.env.example) for all options.

| Variable | Required | Description |
|----------|----------|-------------|
| `GITHUB_TOKEN` | ✅ Yes | GitHub personal access token (repo scope) |
| `GITHUB_OWNER` | ✅ Yes | Your GitHub username |
| `GITHUB_REPOSITORY` | ✅ Yes | Repository name (auto-created if missing) |
| `LLM_PROVIDER` | ❌ No | `openrouter`, `openai`, or `mock` |
| `LLM_API_KEY` | ❌ No | API key for LLM-enhanced research |
| `LLM_MODEL` | ❌ No | Model name (default: `gpt-4o-mini`) |
| `MIN_PUBLICATION_SCORE` | ❌ No | Minimum editorial score (default: `70`) |

### Data Persistence

| Volume | Mount | Content |
|--------|-------|---------|
| `aqevryn_data` | `/app/data` | Moltbook credentials, engagement history |
| `aqevryn_postgres_data` | PostgreSQL | Database (future use) |

---

## Sources

Configure sources in `sources.yaml`. Copy from `sources.example.yaml`:

```bash
cp sources.example.yaml sources.yaml
```

### Source Types

| Type | Description | API Key? |
|------|-------------|----------|
| `rss` | RSS/Atom feeds | No |
| `arxiv` | arXiv research papers | No |
| `hackernews` | Hacker News stories | No |
| `reddit` | Reddit posts | No |
| `github` | GitHub trending repos | GitHub token |
| `moltbook` | Moltbook trending posts | Moltbook API key |

### Built-in Sources (30+ across all domains)

| Domain | Sources |
|--------|---------|
| **Technology** | Hacker News, arXiv, MIT Tech Review, IEEE Spectrum |
| **Science** | Nature, Science Daily, New Scientist, PubMed |
| **Economics** | Bloomberg, Economist, IMF, World Bank |
| **Geopolitics** | Foreign Affairs, War on the Rocks |
| **History** | History Today |
| **Climate** | NASA Climate, Carbon Brief |
| **Energy** | Energy Wire |
| **Business** | Harvard Business Review |
| **Culture** | Aeon, The Conversation |
| **Law** | SCOTUSblog |
| **Community** | Reddit (science, economics, history, geopolitics, + more) |

---

## CLI Commands

| Command | Description |
|---------|-------------|
| `run` | Run the full pipeline (collect → analyze → research → write → review → publish) |
| `discover` | Collect sources and discover topics |
| `analyze` | Analyze trends, researchability, market viability |
| `research` | Conduct deep research |
| `write` | Generate a research article |
| `review` | Run editorial review |
| `publish` | Publish via GitHub PR |
| `scheduler` | Run the scheduler loop |
| `api` | Start the API server |
| `web` | Start the web dashboard (port 9888) |
| `health` | Check application health |
| `dashboard` | Show pipeline activity dashboard |
| `watch` | Live-updating dashboard |
| `logs` | Show recent agent activity log |
| `moltbook` | Interact with Moltbook |
| `test-github` | Test GitHub connection |
| `build-site` | Build the static research website |
| `build-public-site` | Build the public research site |

### Options

| Option | Description |
|--------|-------------|
| `--dry-run` | Run without publishing |
| `-v` | Verbose output |

---

## Pipeline Stages

```
[1/9] Collecting technology sources...   → RSS, arXiv, HN, Reddit, +30 more sources
[2/9] Extracting topics...               → 30+ domain categories (tech, science, economics, etc.)
[3/9] Clustering...                       → Related topics grouped
[4/9] Trend analysis...                   → 5 signals: mentions, growth, sources, recency, diversity
[5/9] Researchability...                  → Source quality, depth, specificity
[6/9] Market viability...                 → ~100 companies tracked
[7/9] Researching...                      → LLM-enhanced deep research (3 phases)
[8/9] Generating article...               → LLM-generated comprehensive paper (11 sections)
[9/9] Editorial review...                 → 6-dimension quality scoring (factual, research, originality, tech, writing, SEO)
     ↓
     Markdown → GitHub PR → (Human approves) → Published
```

---

## Web Dashboard

The web dashboard runs on port **9888** and provides:

| Page | URL | Description |
|------|-----|-------------|
| **Dashboard** | `http://localhost:9888/` | Pipeline summary + recent activity |
| **Articles** | `http://localhost:9888/articles` | 3 tabs: Articles, Topics, Concluded Research |
| **Activity** | `http://localhost:9888/dashboard` | Live agent activity log |
| **API Docs** | `http://localhost:9888/api-docs` | REST API documentation |

### API Endpoints

| Endpoint | Returns |
|----------|---------|
| `GET /api/health` | Health check |
| `GET /api/dashboard` | Pipeline summary + recent activity |
| `GET /api/articles` | All collected articles |
| `GET /api/topics` | Discovered topics |
| `GET /api/completed-research` | All completed research |
| `GET /api/activity` | Agent activity log |

---

## Moltbook Integration

Aqevryn is registered on [Moltbook](https://www.moltbook.com/u/aqevryn) — the social network for AI agents.

### What it does

- **Posts research summaries** after each pipeline run
- **Comments on discussions** using LLM for thoughtful contributions
- **Requests peer review** — posts drafts asking other agents for feedback
- **Browses trending topics** to discover new research opportunities

### CLI Commands

```bash
dotnet Aqevryn.dll moltbook register   # Register the agent
dotnet Aqevryn.dll moltbook status     # Check claim status
dotnet Aqevryn.dll moltbook engage     # Run one engagement cycle
dotnet Aqevryn.dll moltbook post       # Post research to Moltbook
dotnet Aqevryn.dll moltbook feed       # View the feed
dotnet Aqevryn.dll moltbook profile    # View your profile
```

The engagement cycle runs automatically every 60 minutes via the scheduler.

---

## Quality Gates

An article cannot proceed if:

| Gate | Condition | Result |
|------|-----------|--------|
| **Citations** | No references | REJECT |
| **Sources** | Sources cannot be verified | REJECT |
| **Claims** | Major claims unsupported | REJECT |
| **Research Question** | Not answered in article | REVISE |
| **Originality** | Excessive similarity to source | REJECT |
| **Editorial Score** | Below threshold (default 70) | REVISE |
| **Metadata** | Missing slug, title, description | REJECT |
| **Score < 60** | Not recorded as concluded research | Excluded from history |

---

## Architecture

```
                    INTERNET (30+ sources)
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
            │   └───────────────┘   │
            └───────┬───────────────┘
                    │
    ┌───────────────┼───────────────────┐
    │               │                   │
    ▼               ▼                   ▼
  LLM APIs     GitHub API          Moltbook API
  (OpenRouter)  (PRs, Pages)       (Community)
    │               │                   │
    └───────────────┼───────────────────┘
                    │
                    ▼
            ┌────────────────┐
            │  Web Dashboard  │
            │  (port 9888)    │
            └────────────────┘
```

### Project Structure

```
Aqevryn-csharp/
├── src/Aqevryn/
│   ├── Agents/           # 10 research agents
│   │   ├── TopicDiscoveryAgent   # 30+ domain keyword categories
│   │   ├── TopicClusteringAgent  # 14 canonical topic groups
│   │   ├── TrendAnalysisAgent    # 5 deterministic signals
│   │   ├── ResearchabilityAgent  # 3-dimension scoring
│   │   ├── MarketViabilityAgent  # 100+ companies
│   │   ├── ResearchPlannerAgent  # LLM-powered planning
│   │   ├── ResearchAgent         # 3-phase LLM deep research
│   │   ├── ArticleWriterAgent    # LLM-generated comprehensive papers
│   │   ├── EditorialReviewAgent  # 6-dimension quality scoring
│   │   └── TopicRanker           # Configurable weighted scoring
│   ├── Sources/          # 5 source adapters (RSS, arXiv, GitHub, HN, Reddit, Moltbook)
│   ├── Publishing/       # Markdown, GitHub publisher, site generator
│   ├── Common/           # Moltbook client, activity registry, completed research tracker
│   ├── Api/              # Web dashboard, public site generator
│   ├── Scheduler/        # Scheduled pipeline runs + Moltbook engagement
│   ├── Orchestration/    # Full pipeline orchestrator (6 stages)
│   ├── Database/         # EF Core models (12 entities)
│   ├── Config/           # Environment-based configuration
│   └── Program.cs        # CLI with 20+ commands
├── tests/                # xUnit tests
├── Dockerfile
├── docker-compose.yml
├── soul.md               # Agent's core identity (52 sections)
├── MANUAL.md             # Complete user manual
├── DEPLOY.md             # Production deployment guide
└── .env.example
```

---

## Deployment

### Single Server

```bash
docker compose up -d --build
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll run"
```

See [DEPLOY.md](DEPLOY.md) for production deployment and migration.

### Public Website

To serve the public research site at `agents.aqevryn.me`:

```bash
# Generate the site
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll build-public-site"

# Output: public-site/ directory with all research papers
```

See [WEBSITE.md](WEBSITE.md) for nginx configuration.

---

## Tests

```bash
dotnet test
```

---

## License

MIT

---

## The Agent's Soul

Aqevryn operates according to strict research principles defined in [`soul.md`](soul.md). Key tenets:

- **Truth over narrative** — Evidence determines conclusions, not the other way around
- **Uncertainty over false confidence** — "Insufficient evidence" is a valid conclusion
- **Quality over speed** — One excellent article > ten shallow ones
- **Independence** — Not popularity-driven, not SEO-driven, not quota-driven
- **Multi-disciplinary** — Research across technology, science, economics, history, culture, and more
- **Evidence chain preserved** — Every claim traceable to its source
- **Falsifiability** — Actively search for evidence against emerging conclusions
- **No fabrication** — Never fabricate sources, citations, statistics, or data

---

*Built with .NET 8, Docker, and a lot of curiosity.*