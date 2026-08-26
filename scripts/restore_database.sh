#!/usr/bin/env bash
# Aqevryn C# — PostgreSQL Database Restore Script
# Usage: ./scripts/restore_database.sh <backup_file>
set -euo pipefail
if [ $# -lt 1 ]; then echo "Usage: $0 <backup_file>"; exit 1; fi
BACKUP_FILE="$1"
if [ ! -f "$BACKUP_FILE" ]; then echo "ERROR: File not found: $BACKUP_FILE"; exit 1; fi
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
if [ -f "$PROJECT_DIR/.env" ]; then set -a; source "$PROJECT_DIR/.env"; set +a; fi
DB_CONTAINER="${DB_CONTAINER:-aqevryn-postgres}"
DB_USER="${DB_USER:-aqevryn}"
DB_NAME="${DB_NAME:-aqevryn}"
RESTORE_FILE="$BACKUP_FILE"
if [[ "$BACKUP_FILE" == *.gz ]]; then RESTORE_FILE="${BACKUP_FILE%.gz}"; gunzip -k -f "$BACKUP_FILE"; fi
echo "WARNING: This will overwrite the database! Press Ctrl+C to cancel or Enter to continue..."; read -r
docker exec -i "$DB_CONTAINER" pg_restore -U "$DB_USER" -d "$DB_NAME" --clean --if-exists < "$RESTORE_FILE"
if [[ "$BACKUP_FILE" != "$RESTORE_FILE" ]]; then rm -f "$RESTORE_FILE"; fi
echo "Restore complete."