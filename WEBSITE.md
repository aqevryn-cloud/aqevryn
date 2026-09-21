# Aqevryn — Public Research Website

> **Domain:** https://agents.aqevryn.me
> **Purpose:** Publicly showcase all concluded research papers with full content, methodology, and findings.

---

## Quick Setup

The web dashboard already serves research content. To make it publicly accessible at `agents.aqevryn.me`:

### Option 1: Direct Access (via reverse proxy)

Run the web dashboard on port 80 directly:

```bash
# Stop any existing aqevryn processes on port 80
sudo systemctl stop nginx 2>/dev/null

# Restart with port 80 exposed
docker compose down
# Edit docker-compose.yml: change "127.0.0.1:9888:9888" to "0.0.0.0:80:9888" or "9888:9888"
docker compose up -d
```

### Option 2: Nginx Reverse Proxy (recommended)

```bash
# Install nginx
sudo apt install -y nginx

# Create config
sudo tee /etc/nginx/sites-available/aqevryn << 'EOF'
server {
    listen 80;
    server_name agents.aqevryn.me;

    location / {
        proxy_pass http://127.0.0.1:9888;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF

# Enable site
sudo ln -sf /etc/nginx/sites-available/aqevryn /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

## What's Already Available

The web dashboard at port 9888 already serves:

| Page | URL | Content |
|------|-----|---------|
| **Dashboard** | `http://agents.aqevryn.me/` | Pipeline summary, recent activity |
| **Articles** | `http://agents.aqevryn.me/articles` | Collected articles (3 tabs: Articles, Topics, Concluded Research) |
| **Concluded Research** | `http://agents.aqevryn.me/articles` (Completed tab) | All completed research with scores, findings, PR links |
| **Activity** | `http://agents.aqevryn.me/dashboard` | Agent activity log |
| **API** | `http://agents.aqevryn.me/api/completed-research` | JSON data of all completed research |

---

## Data Flow

```
Pipeline completes research
        ↓
CompletedResearchRegistry records it
        ↓
Data saved to /app/data/completed_research.json
        ↓
Web dashboard reads /api/completed-research
        ↓
Browser displays results at agents.aqevryn.me/articles
```