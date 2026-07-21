#!/usr/bin/env bash
#
# ByteBazaar — PostgreSQL logical backup.
#
# Produces a compressed custom-format dump (pg_dump -Fc), which is what
# scripts/restore-verify.sh consumes. Custom format is used rather than plain
# SQL because it supports parallel restore and selective object restore, and
# pg_restore can list its contents for verification without applying anything.
#
# Runs pg_dump *inside* the Postgres container so the client version always
# matches the server and no local libpq install is required.
#
# Usage:
#   scripts/backup-postgres.sh [output-dir]
#
# Env (all optional, defaults match docker-compose.yml):
#   PG_CONTAINER   container name        (default bytebazaar-postgres)
#   PG_USER        superuser             (default bytebazaar)
#   PG_DB          database to dump      (default bytebazaar)
#   BACKUP_DIR     output directory      (default ./backups)
#   RETENTION_DAYS prune dumps older than (default 14; 0 disables pruning)
#
# On Windows this runs under Git Bash. MSYS_NO_PATHCONV=1 is set because MSYS
# rewrites container-side absolute paths in `docker exec` arguments.
set -euo pipefail
export MSYS_NO_PATHCONV=1

PG_CONTAINER="${PG_CONTAINER:-bytebazaar-postgres}"
PG_USER="${PG_USER:-bytebazaar}"
PG_DB="${PG_DB:-bytebazaar}"
BACKUP_DIR="${1:-${BACKUP_DIR:-./backups}}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="${BACKUP_DIR}/bytebazaar-${STAMP}.dump"

mkdir -p "$BACKUP_DIR"

if ! docker inspect -f '{{.State.Running}}' "$PG_CONTAINER" >/dev/null 2>&1; then
  echo "ERROR: container '$PG_CONTAINER' is not running." >&2
  exit 1
fi

echo "==> Dumping '${PG_DB}' from container '${PG_CONTAINER}'"
# --clean --if-exists so the dump can be replayed over a non-empty target;
# --no-owner/--no-privileges so it restores cleanly under a different role
# (e.g. into the throwaway verification database).
docker exec "$PG_CONTAINER" pg_dump \
  -U "$PG_USER" \
  -d "$PG_DB" \
  --format=custom \
  --compress=9 \
  --clean --if-exists \
  --no-owner --no-privileges \
  > "$OUT"

SIZE=$(wc -c < "$OUT" | tr -d ' ')
if [ "$SIZE" -lt 1024 ]; then
  echo "ERROR: dump is only ${SIZE} bytes — treating as failed." >&2
  rm -f "$OUT"
  exit 1
fi

# Integrity gate: pg_restore -l parses the dump's table of contents. If the
# archive is truncated or corrupt this fails, so a backup that cannot be listed
# never gets recorded as successful.
docker run --rm -i postgres:16-alpine pg_restore -l < "$OUT" > "${OUT}.toc"
ENTRIES=$(grep -c ';' "${OUT}.toc" || true)

echo "==> OK  ${OUT}"
echo "    size:        ${SIZE} bytes"
echo "    toc entries: ${ENTRIES}"

if [ "$RETENTION_DAYS" -gt 0 ]; then
  echo "==> Pruning dumps older than ${RETENTION_DAYS} days"
  find "$BACKUP_DIR" -name 'bytebazaar-*.dump*' -type f -mtime "+${RETENTION_DAYS}" -print -delete || true
fi

echo "$OUT"
