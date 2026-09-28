<!-- Aqevryn - README.md -->
<!-- GitHub Repository Landing Page -->

<div align="center">

# 🦀 **Aqevryn**

### *Autonomous Research & Intelligence Organization*

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-✓-2496ED?logo=docker)](https://docker.com/)
[![Moltbook](https://img.shields.io/badge/Moltbook-🦞-00d4aa)](https://www.moltbook.com/u/aqevryn)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-48%20passing-success)](https://github.com/aqevryn-cloud/aqevryn/actions)

*Discover important questions. Investigate evidence. Develop knowledge. Publish rigorously across all disciplines.*

</div>

---

## 📋 **Concluded Research**

Click any topic to read the full research paper.

| # | Topic | Question | Score | Findings | Date |
|---|-------|----------|-------|----------|------|
| 1 | **[Cybersecurity](https://github.com/aqevryn-cloud/aqevryn/projects)** | Mitigating risks in modern infrastructure | 78.5 | 10 findings | 2026-09 |
| 2 | **[AI Agents](https://github.com/aqevryn-cloud/aqevryn/projects)** | Failure modes in production environments | 84.8 | 8 findings | 2026-09 |
| 3 | **[Augmented & Virtual Reality](https://github.com/aqevryn-cloud/aqevryn/projects)** | Key drivers and limitations | 84.8 | 17 findings | 2026-09 |
| 4 | **[Emerging Technologies](https://github.com/aqevryn-cloud/aqevryn/projects)** | Adoption factors and barriers | 80.8 | 10 findings | 2026-09 |
| 5 | **[Web Technologies](https://github.com/aqevryn-cloud/aqevryn/projects)** | Current state and future trajectory | 79.5 | 10 findings | 2026-09 |
| 6 | **[Robotics](https://github.com/aqevryn-cloud/aqevryn/projects)** | Deployment readiness comparison | 80.5 | 8 findings | 2026-09 |
| 7 | **[Distributed Systems](https://github.com/aqevryn-cloud/aqevryn/projects)** | Impact factors on performance | 80.5 | 10 findings | 2026-09 |
| 8 | **[Artificial Intelligence](https://github.com/aqevryn-cloud/aqevryn/projects)** | Adoption drivers and limitations | 80.0 | 15 findings | 2026-09 |
| 9 | **[Software Engineering](https://github.com/aqevryn-cloud/aqevryn/projects)** | Key trends and practices | 81.0 | 8 findings | 2026-09 |
| 10 | **[Cloud Computing](https://github.com/aqevryn-cloud/aqevryn/projects)** | Operational efficiency analysis | 79.0 | 13 findings | 2026-09 |
| 11 | **[Networking](https://github.com/aqevryn-cloud/aqevryn/projects)** | Evolution and performance impact | 81.0 | 12 findings | 2026-09 |
| 12 | **[Energy Technology](https://github.com/aqevryn-cloud/aqevryn/projects)** | Key adoption factors | 80.5 | 10 findings | 2026-09 |
| 13 | **[Databases](https://github.com/aqevryn-cloud/aqevryn/projects)** | Performance and scalability | 80.5 | 10 findings | 2026-09 |
| 14 | **[Semiconductors](https://github.com/aqevryn-cloud/aqevryn/projects)** | Commercial adoption barriers | 79.5 | 8 findings | 2026-09 |

> 📊 **Live dashboard:** Visit the [Aqevryn Web Dashboard](https://github.com/aqevryn-cloud/aqevryn) for real-time pipeline data.

---

## 🧭 **What Aqevryn Researches**

Aqevryn is **domain-agnostic**. Per its [soul.md](soul.md), it researches across all disciplines:

| Domain | Sources | Status |
|--------|---------|--------|
| 🖥️ **Technology** | HN, arXiv, IEEE, MIT Tech | ✅ Active |
| 🔬 **Science** | Nature, Science Daily, New Scientist | ✅ Active |
| 💰 **Economics** | Bloomberg, Economist, IMF, World Bank | ✅ Active |
| 📈 **Finance** | WSJ, market feeds | ✅ Active |
| 🏛️ **History** | History Today, academic feeds | ✅ Active |
| 🌍 **Geopolitics** | Foreign Affairs, War on the Rocks | ✅ Active |
| 🌡️ **Climate** | NASA Climate, Carbon Brief | ✅ Active |
| ⚡ **Energy** | Energy Wire, industry feeds | ✅ Active |
| 🏢 **Business** | HBR, business feeds | ✅ Active |
| ⚖️ **Law** | SCOTUSblog, policy feeds | ✅ Active |
| 🎭 **Culture** | Aeon, The Conversation | ✅ Active |
| 🧠 **Psychology** | Academic & research feeds | ✅ Active |

---

## 🏗️ **Architecture**

```
INTERNET (30+ sources across 12 domains)
    │
    ▼
┌─────────────────────────────────┐
│         Aqevryn Pipeline         │
│  Collect → Discover → Analyze   │
│  → Rank → Research → Write     │
│  → Review → Publish             │
└──────────┬──────────────────────┘
           │
    ┌──────┴──────┐
    │   LLM API   │  OpenRouter / OpenAI
    │  GitHub API │  PRs & Pages
    │ Moltbook API│  Community engagement
    └─────────────┘
```

---

## 🤖 **Community & Collaboration**

Aqevryn is an active member of the [Moltbook](https://www.moltbook.com/u/aqevryn) AI agent community:

- 🦞 **Profile:** [https://www.moltbook.com/u/aqevryn](https://www.moltbook.com/u/aqevryn)
- 💬 **Comments** on trending discussions with research-backed perspectives
- 📝 **Posts** research summaries and asks for **peer review** from other agents
- 🔍 **Discovers** new research topics from community conversations

---

## ⚡ **Quick Start**

```bash
# Clone
git clone https://github.com/aqevryn-cloud/aqevryn.git
cd aqevryn

# Configure
cp .env.example .env
cp sources.example.yaml sources.yaml

# Run (development)
dotnet run --project src/Aqevryn -- --dry-run run

# Run (production with Docker)
docker compose up -d
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll run"
```

---

## 📚 **Documentation**

| Document | Description |
|----------|-------------|
| **[soul.md](soul.md)** | Agent's core identity, principles, and philosophy (52 sections) |
| **[MANUAL.md](MANUAL.md)** | Complete user manual covering all features |
| **[DEPLOY.md](DEPLOY.md)** | Production deployment and migration guide |
| **[WEBSITE.md](WEBSITE.md)** | Public website setup for `agents.aqevryn.me` |

---

## 📊 **Project Stats**

| Metric | Value |
|--------|-------|
| Research Papers | 14 completed |
| Sources | 30+ across 12 domains |
| Topic Categories | 35+ keyword groups |
| Tests | 48 passing |
| Language | C# (.NET 8) |
| Container Size | ~269MB |

---

## 📜 **License**

MIT — See [LICENSE](LICENSE)

---

<div align="center">

**Built with curiosity, evidence, and .NET 8**

_One rigorous investigation > a hundred shallow articles_

</div>