#!/usr/bin/env bash
# Aqevryn C# — PostgreSQL Database Backup Script
# Usage: ./scripts/backup_database.sh [output_dir]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
if [ -f "$PROJECT_DIR/.env" ]; then set -a; source "$PROJECT_DIR/.env"; set +a; fi
DB_CONTAINER="${DB_CONTAINER:-aqevryn-postgres}"
DB_USER="${DB_USER:-aqevryn}"
DB_NAME="${DB_NAME:-aqevryn}"
BACKUP_DIR="${1:-$PROJECT_DIR/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/aqevryn_${TIMESTAMP}.dump.gz"
mkdir -p "$BACKUP_DIR"
if ! docker ps --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then echo "ERROR: Container ${DB_CONTAINER} not running."; exit 1; fi
echo "Creating backup of ${DB_NAME} from ${DB_CONTAINER}..."
docker exec "$DB_CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" --format=custom --compress=9 -f "/tmp/backup_${TIMESTAMP}.dump"
docker cp "${DB_CONTAINER}:/tmp/backup_${TIMESTAMP}.dump" "${BACKUP_DIR}/aqevryn_${TIMESTAMP}.dump"
docker exec "$DB_CONTAINER" rm -f "/tmp/backup_${TIMESTAMP}.dump"
gzip -f "${BACKUP_DIR}/aqevryn_${TIMESTAMP}.dump"
find "$BACKUP_DIR" -name "aqevryn_*.dump.gz" -mtime "+${RETENTION_DAYS}" -delete 2>/dev/null || true
echo "Backup saved: ${BACKUP_FILE} ($(du -h "$BACKUP_FILE" | cut -f1))"