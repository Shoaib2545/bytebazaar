#!/usr/bin/env bash
#
# ByteBazaar — restore drill.
#
# PLAN.md M7 requires the backup be *proven restorable*, not merely taken.
# This script restores a dump into a THROWAWAY database and asserts that the
# restored data matches the source, then drops the throwaway database.
#
# SAFETY: the target database name is forced to end in `_restore_verify` and the
# script refuses to run if the target equals the live database. It never writes
# to, drops, or truncates the live `bytebazaar` database — it only reads row
# counts from it for comparison.
#
# Usage:
#   scripts/restore-verify.sh <path-to-.dump>
#   scripts/restore-verify.sh            # uses the newest dump in ./backups
#
# Env:
#   PG_CONTAINER  (default bytebazaar-postgres)
#   PG_USER       (default bytebazaar)
#   PG_DB         live db, read-only here (default bytebazaar)
#   KEEP_RESTORE  set to 1 to leave the throwaway db in place for inspection
set -euo pipefail
export MSYS_NO_PATHCONV=1

PG_CONTAINER="${PG_CONTAINER:-bytebazaar-postgres}"
PG_USER="${PG_USER:-bytebazaar}"
PG_DB="${PG_DB:-bytebazaar}"
TARGET_DB="bytebazaar_restore_verify"
BACKUP_DIR="${BACKUP_DIR:-./backups}"

if [ "$TARGET_DB" = "$PG_DB" ]; then
  echo "FATAL: refusing to restore over the live database." >&2
  exit 1
fi

DUMP="${1:-}"
if [ -z "$DUMP" ]; then
  DUMP=$(ls -1t "${BACKUP_DIR}"/bytebazaar-*.dump 2>/dev/null | head -1 || true)
fi
[ -n "$DUMP" ] && [ -f "$DUMP" ] || { echo "FATAL: no dump file found (looked in ${BACKUP_DIR})" >&2; exit 1; }

psql_live() { docker exec -i "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -tAc "$1"; }
psql_tgt()  { docker exec -i "$PG_CONTAINER" psql -U "$PG_USER" -d "$TARGET_DB" -tAc "$1"; }
psql_admin(){ docker exec -i "$PG_CONTAINER" psql -U "$PG_USER" -d postgres -tAc "$1"; }

echo "==> Restore drill"
echo "    dump:      $DUMP"
echo "    source db: $PG_DB (read-only)"
echo "    target db: $TARGET_DB (throwaway)"
echo

# Tables whose row counts must survive the round trip. These are the ones that
# actually matter for a recovery: losing any of them is data loss, not cosmetic.
TABLES='Categories AttributeDefinitions Brands Products Orders OrderItems AspNetUsers'

echo "==> Source row counts"
declare -A BEFORE
for t in $TABLES; do
  n=$(psql_live "SELECT count(*) FROM \"$t\";" | tr -d '\r')
  BEFORE[$t]=$n
  printf '    %-22s %s\n' "$t" "$n"
done
echo

echo "==> (Re)creating throwaway database"
psql_admin "DROP DATABASE IF EXISTS \"$TARGET_DB\" WITH (FORCE);" >/dev/null
psql_admin "CREATE DATABASE \"$TARGET_DB\";" >/dev/null

echo "==> Restoring"
# --no-owner/--no-privileges: the dump was taken that way; --exit-on-error so a
# partial restore is a hard failure rather than a silently half-populated db.
docker exec -i "$PG_CONTAINER" pg_restore \
  -U "$PG_USER" -d "$TARGET_DB" \
  --no-owner --no-privileges --exit-on-error \
  < "$DUMP"
echo "    pg_restore exited 0"
echo

echo "==> Verifying restored row counts"
FAIL=0
for t in $TABLES; do
  n=$(psql_tgt "SELECT count(*) FROM \"$t\";" | tr -d '\r')
  if [ "$n" = "${BEFORE[$t]}" ]; then
    printf '    OK   %-22s %s\n' "$t" "$n"
  else
    printf '    FAIL %-22s expected %s got %s\n' "$t" "${BEFORE[$t]}" "$n"
    FAIL=1
  fi
done
echo

# Structural checks: a row-count match can still hide a lost index. The jsonb
# GIN index on Products.Attributes is what makes the dynamic filter engine fast,
# so its absence after a restore would be a silent performance cliff.
echo "==> Verifying schema objects"
GIN=$(psql_tgt "SELECT count(*) FROM pg_indexes WHERE tablename='Products' AND indexdef ILIKE '%gin%';" | tr -d '\r')
if [ "$GIN" -ge 1 ]; then echo "    OK   jsonb GIN index on Products present ($GIN)"; else echo "    FAIL jsonb GIN index on Products missing"; FAIL=1; fi

FKS=$(psql_tgt "SELECT count(*) FROM pg_constraint WHERE contype='f';" | tr -d '\r')
FKS_SRC=$(psql_live "SELECT count(*) FROM pg_constraint WHERE contype='f';" | tr -d '\r')
if [ "$FKS" = "$FKS_SRC" ]; then echo "    OK   foreign keys: $FKS"; else echo "    FAIL foreign keys: expected $FKS_SRC got $FKS"; FAIL=1; fi

MIG=$(psql_tgt "SELECT count(*) FROM \"__EFMigrationsHistory\";" | tr -d '\r')
MIG_SRC=$(psql_live "SELECT count(*) FROM \"__EFMigrationsHistory\";" | tr -d '\r')
if [ "$MIG" = "$MIG_SRC" ]; then echo "    OK   EF migrations applied: $MIG"; else echo "    FAIL EF migrations: expected $MIG_SRC got $MIG"; FAIL=1; fi

# Data-level spot check: the dynamic attribute payload is stored as jsonb; prove
# it came back queryable, not just present as text.
echo
echo "==> Spot-checking dynamic attributes (jsonb containment)"
JQ=$(psql_tgt "SELECT count(*) FROM \"Products\" WHERE \"Attributes\" @> '{\"ram\":\"16GB\"}'::jsonb;" | tr -d '\r')
JQ_SRC=$(psql_live "SELECT count(*) FROM \"Products\" WHERE \"Attributes\" @> '{\"ram\":\"16GB\"}'::jsonb;" | tr -d '\r')
if [ "$JQ" = "$JQ_SRC" ]; then echo "    OK   products with ram=16GB: $JQ"; else echo "    FAIL ram=16GB: expected $JQ_SRC got $JQ"; FAIL=1; fi

echo
if [ "${KEEP_RESTORE:-0}" = "1" ]; then
  echo "==> KEEP_RESTORE=1, leaving $TARGET_DB in place"
else
  echo "==> Dropping throwaway database"
  psql_admin "DROP DATABASE IF EXISTS \"$TARGET_DB\" WITH (FORCE);" >/dev/null
fi

if [ "$FAIL" -eq 0 ]; then
  echo
  echo "RESTORE DRILL PASSED"
else
  echo
  echo "RESTORE DRILL FAILED"
  exit 1
fi
