# Aqevryn — Production Deployment Guide

> **How to migrate Aqevryn to a new production server**

---

## Step 1: Package the Project

On your **current server**, create a deployment archive:

```bash
cd /home/admin1/Aqevryn-csharp

# Create a clean archive (excludes git history, build artifacts)
tar -czf aqevryn-deploy.tar.gz \
  --exclude='.git' \
  --exclude='bin' \
  --exclude='obj' \
  --exclude='node_modules' \
  src/Aqevryn/Aqevryn.csproj \
  src/Aqevryn/Program.cs \
  src/Aqevryn/Agents/ \
  src/Aqevryn/Api/ \
  src/Aqevryn/Common/ \
  src/Aqevryn/Config/ \
  src/Aqevryn/Database/ \
  src/Aqevryn/Orchestration/ \
  src/Aqevryn/Publishing/ \
  src/Aqevryn/Research/ \
  src/Aqevryn/Scheduler/ \
  src/Aqevryn/Sources/ \
  Dockerfile \
  docker-compose.yml \
  .env.example \
  sources.example.yaml \
  .github/ \
  scripts/ \
  soul.md \
  MANUAL.md

# Check the size
ls -lh aqevryn-deploy.tar.gz
```

---

## Step 2: Transfer to the New Server

```bash
# Option A: Using SCP (best for small transfers)
scp aqevryn-deploy.tar.gz user@your-new-server-ip:/home/user/

# Option B: Using rsync (better for large directories)
rsync -avz --progress \
  --exclude='.git' \
  --exclude='bin' \
  --exclude='obj' \
  /home/admin1/Aqevryn-csharp/ \
  user@your-new-server-ip:/home/user/Aqevryn-csharp/

# Option C: Using Git (recommended for ongoing development)
# First, push to GitHub
git remote add origin https://github.com/aqevryn-cloud/aqevryn.git
git push -u origin master

# Then on the new server
git clone https://github.com/aqevryn-cloud/aqevryn.git
cd aqevryn
```

---

## Step 3: Set Up the New Server

### Prerequisites

```bash
# SSH into your new server
ssh user@your-new-server-ip

# Update system packages
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
# Log out and back in for group changes to take effect

# Install Docker Compose
sudo apt install -y docker-compose-plugin

# Verify
docker --version
docker compose version
```

### Deploy Aqevryn

```bash
# Extract the archive (if using SCP)
cd /home/user
tar -xzf aqevryn-deploy.tar.gz
cd Aqevryn-csharp

# Configure environment
cp .env.example .env
nano .env
```

---

## Step 4: Configure `.env` for Production

```ini
# --- Application ---
APP_ENV=production
LOG_LEVEL=INFO

# --- LLM Provider (OpenRouter with your existing key) ---
LLM_PROVIDER=openrouter
LLM_API_KEY=sk-or-v1-your-key-here
LLM_MODEL=gpt-4o-mini
LLM_MAX_TOKENS=16384
LLM_TEMPERATURE=0.3

# --- GitHub ---
GITHUB_TOKEN=ghp_your-token-here
GITHUB_OWNER=aqevryn-cloud
GITHUB_REPOSITORY=aqevryn
GITHUB_DEFAULT_BRANCH=main

# --- Git Committer Identity ---
GIT_COMMITTER_NAME=Aqevryn Research
GIT_COMMITTER_EMAIL=aqevryn@gmail.com

# --- Database ---
DATABASE_URL=Host=postgres;Database=aqevryn;Username=aqevryn;Password=aqevryn

# --- Pipeline ---
AUTO_PUBLISH=false
MIN_PUBLICATION_SCORE=70
DRY_RUN=false

# --- Scoring Weights ---
WEIGHT_TREND=0.25
WEIGHT_RESEARCHABILITY=0.25
WEIGHT_MARKET=0.20
WEIGHT_NOVELTY=0.15
WEIGHT_TECHNICAL_SIGNIFICANCE=0.15

# --- Scheduler ---
COLLECT_INTERVAL_HOURS=6
ANALYZE_INTERVAL_HOURS=12
RANK_INTERVAL_HOURS=24

# --- Cost Controls ---
MAX_SOURCES_PER_RESEARCH=30
MAX_RETRIES=3
REQUEST_TIMEOUT_SECONDS=60

# --- Email Notifications (optional) ---
# SMTP_HOST=smtp.gmail.com
# SMTP_PORT=587
# SMTP_USER=aqevryn@gmail.com
# SMTP_PASS=your-16-char-app-password
```

---

## Step 5: Configure Sources

```bash
cp sources.example.yaml sources.yaml
nano sources.yaml
```

For production, you may want to enable more sources. The default config includes:
- Hacker News (free)
- arXiv (free)
- GitHub Trending (needs your GITHUB_TOKEN)
- Reddit (free)
- RSS feeds (free)

---

## Step 6: Restore Moltbook Credentials

If you were using Moltbook on the previous server, create the credentials file on the new server:

```bash
mkdir -p data
cat > data/moltbook_credentials.json << 'EOF'
{"api_key": "moltbook_sk_Ro5951BxPQ9hS23z83Sq2bFNIKN6d2eg", "agent_name": "Aqevryn"}
EOF
```

---

## Step 7: Start the Services

```bash
# Build and start
docker compose up -d --build

# Check that everything is running
docker compose ps

# Expected output:
# NAME                STATUS
# aqevryn             Up (running scheduler)
# aqevryn-postgres    Up (healthy)

# View logs
docker compose logs -f aqevryn
```

---

## Step 8: Verify Deployment

```bash
# Health check
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll health"

# Test GitHub connection
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll test-github"

# Run the pipeline in dry-run mode
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll --dry-run run"

# View the dashboard
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll dashboard"

# Check Moltbook status
docker exec aqevryn sh -c "cd /app && dotnet Aqevryn.dll moltbook status"
```

---

## Step 9: Access the Web Dashboard

```bash
# From your local machine, SSH tunnel to the new server
ssh -L 9888:localhost:9888 user@your-new-server-ip

# Then open http://localhost:9888 in your browser
```

---

## Step 10: Set Up for Continuous Operation

```bash
# Make Docker start on boot
sudo systemctl enable docker

# Check that the container restarts automatically
# (docker-compose.yml has 'restart: unless-stopped')
docker inspect aqevryn | grep RestartPolicy

# The scheduler runs automatically when the container starts
# It will:
#   - Collect sources every 6 hours
#   - Analyze trends every 12 hours
#   - Run full research every 24 hours
```

---

## One-Line Migration Commands

### Package on old server:
```bash
cd /home/admin1/Aqevryn-csharp && tar -czf /tmp/aqevryn-deploy.tar.gz --exclude='.git' --exclude='bin' --exclude='obj' . && scp /tmp/aqevryn-deploy.tar.gz user@new-server:/home/user/
```

### Deploy on new server:
```bash
cd /home/user && tar -xzf aqevryn-deploy.tar.gz && cd Aqevryn-csharp && cp .env.example .env && nano .env && docker compose up -d --build && docker compose logs -f aqevryn
```

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `Docker not found` | Install Docker: `curl -fsSL https://get.docker.com | sh` |
| `Permission denied` | Add user to docker group: `sudo usermod -aG docker $USER` |
| Port 9888 already in use | Change the port in `docker-compose.yml` and `Program.cs` |
| Pipeline fails at research | Check `LLM_API_KEY` is set correctly |
| Pipeline fails at publish | Check `GITHUB_TOKEN` has `repo` scope |
| Database connection refused | Wait for postgres to be healthy: `docker compose logs postgres` |
| Moltbook not registered | Run: `dotnet Aqevryn.dll moltbook register` |

---

## Important Notes

- **The Moltbook API key** is stored in `data/moltbook_credentials.json` — this is on a Docker volume and will persist across rebuilds
- **The database** is stored in Docker volume `aqevryn_postgres_data` — persists across rebuilds
- **The `.env` file** contains secrets — never commit it to Git
- **The `.env.example` file** is safe to commit — it has placeholder values only
- **`--dry-run`** flag is your friend — test everything before running without it