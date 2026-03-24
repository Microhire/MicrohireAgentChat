#!/usr/bin/env bash
# Delete all app + Microhire (AITESTDB) data for one email address:
# contacts, bookings, contact–org links, removable organisations (tblcust), AgentThreads (UserKey).
# Usage:
#   export AITEST_PASSWORD=... INTENT_PASSWORD=...   # or source Scripts/delete-email.env
#   ./Scripts/delete-email-data.sh 'nith@intent.do'
#
# Requires: sqlcmd (brew install sqlcmd), network access to SQL Server / Azure SQL.

set -euo pipefail

EMAIL="${1:-nith@intent.do}"

if [[ ! "$EMAIL" =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; then
  echo "Invalid email: $EMAIL" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPS_DIR="$SCRIPT_DIR/../MicrohireAgentChat"

load_cs_password() {
  local key="$1" file="$2"
  [[ -f "$file" ]] || return 1
  python3 -c "
import json, sys
with open(sys.argv[1], encoding='utf-8') as f:
    cs = json.load(f)['ConnectionStrings'].get(sys.argv[2], '') or ''
for part in cs.split(';'):
    p = part.strip()
    low = p.lower()
    if low.startswith('password='):
        print(p.split('=', 1)[1].strip())
        sys.exit(0)
sys.exit(1)
" "$file" "$key" 2>/dev/null
}

# True if value is empty or still the example placeholder (common mistake after cp delete-email.env.example)
is_bad_password() {
  local p="${1:-}"
  [[ -z "$p" ]] && return 0
  case "$p" in
    *PASTE_*|*PLACEHOLDER*|example|changeme) return 0 ;;
  esac
  return 1
}

# 1) Cache passwords from appsettings (dotnet app uses the same ConnectionStrings)
_PW_AITEST_LOCAL="$(load_cs_password BookingsDb "$APPS_DIR/appsettings.Development.Local.json" 2>/dev/null || true)"
_PW_AITEST_JSON="$(load_cs_password BookingsDb "$APPS_DIR/appsettings.json" 2>/dev/null || true)"
_PW_INTENT_LOCAL="$(load_cs_password AppConnection "$APPS_DIR/appsettings.Development.Local.json" 2>/dev/null || true)"
_PW_INTENT_JSON="$(load_cs_password AppConnection "$APPS_DIR/appsettings.json" 2>/dev/null || true)"

resolve_aitest_password() {
  if ! is_bad_password "${AITEST_PASSWORD:-}"; then
    return
  fi
  if ! is_bad_password "$_PW_AITEST_LOCAL"; then AITEST_PASSWORD="$_PW_AITEST_LOCAL"; return; fi
  if ! is_bad_password "$_PW_AITEST_JSON"; then AITEST_PASSWORD="$_PW_AITEST_JSON"; return; fi
  AITEST_PASSWORD=""
}

resolve_intent_password() {
  if ! is_bad_password "${INTENT_PASSWORD:-}"; then
    return
  fi
  if ! is_bad_password "$_PW_INTENT_LOCAL"; then INTENT_PASSWORD="$_PW_INTENT_LOCAL"; return; fi
  if ! is_bad_password "$_PW_INTENT_JSON"; then INTENT_PASSWORD="$_PW_INTENT_JSON"; return; fi
  INTENT_PASSWORD=""
}

AITEST_PASSWORD=""
INTENT_PASSWORD=""

# 2) Optional: host / user / password overrides (sourced file may leave passwords blank → use appsettings)
if [[ -f "$SCRIPT_DIR/delete-email.env" ]]; then
  # shellcheck source=/dev/null
  source "$SCRIPT_DIR/delete-email.env"
fi

AITEST_HOST="${AITEST_HOST:-tcp:116.90.5.144,41383}"
AITEST_DB="${AITEST_DB:-AITESTDB}"
AITEST_USER="${AITEST_USER:-PowerBI-Consult}"
INTENT_HOST="${INTENT_HOST:-tcp:intenttest.database.windows.net,1433}"
INTENT_DB="${INTENT_DB:-IntentTestDB}"
INTENT_USER="${INTENT_USER:-azadmin}"

resolve_aitest_password
resolve_intent_password

if [[ -z "${AITEST_PASSWORD:-}" ]]; then
  echo "AITEST_PASSWORD is empty. Set ConnectionStrings:BookingsDb in MicrohireAgentChat/appsettings.json" >&2
  echo "or put the real Password= value in Scripts/delete-email.env" >&2
  exit 1
fi
if [[ -z "${INTENT_PASSWORD:-}" ]]; then
  echo "INTENT_PASSWORD is empty. Set AppConnection in appsettings or Scripts/delete-email.env" >&2
  exit 1
fi

SQLCMD_BIN="$(command -v sqlcmd)"
if [[ -z "$SQLCMD_BIN" ]]; then
  echo "sqlcmd not found. Install: brew install sqlcmd" >&2
  exit 1
fi

# Escape single quotes for T-SQL string literals (double each ')
EMAIL_SQL="${EMAIL//\'/\'\'}"


run_aitest_sql() {
  SQLCMDPASSWORD="$AITEST_PASSWORD" "$SQLCMD_BIN" -S "$AITEST_HOST" -d "$AITEST_DB" -U "$AITEST_USER" -C -Q "$1"
}

run_intent_sql() {
  SQLCMDPASSWORD="$INTENT_PASSWORD" "$SQLCMD_BIN" -S "$INTENT_HOST" -d "$INTENT_DB" -U "$INTENT_USER" -C -N -Q "$1"
}

echo "=== Deleting data for: $EMAIL ==="

echo "--- AITESTDB ($AITEST_DB): bookings, tblcust orgs for user, AgentThreads ---"
run_aitest_sql "
SET NOCOUNT ON;
BEGIN TRANSACTION;
DECLARE @TargetEmail NVARCHAR(200) = N'$EMAIL_SQL';
DECLARE @ContactIds TABLE (id DECIMAL(10,0));
INSERT INTO @ContactIds SELECT ID FROM dbo.tblContact WHERE LOWER(LTRIM(Email)) = LOWER(@TargetEmail);

DECLARE @OrgIds TABLE (id DECIMAL(10,0));
INSERT INTO @OrgIds SELECT DISTINCT b.CustID FROM dbo.tblbookings b
  INNER JOIN @ContactIds c ON b.ContactID = c.id WHERE b.CustID IS NOT NULL;
INSERT INTO @OrgIds SELECT DISTINCT ID FROM dbo.tblcust WHERE iLink_ContactID IN (SELECT id FROM @ContactIds);
INSERT INTO @OrgIds SELECT DISTINCT c.ID FROM dbo.tblLinkCustContact l
  INNER JOIN dbo.tblcust c ON c.Customer_code IS NOT NULL AND l.Customer_Code IS NOT NULL AND l.Customer_Code = c.Customer_code
  WHERE l.ContactID IN (SELECT id FROM @ContactIds);

DELETE it FROM dbo.tblitemtran it
WHERE it.booking_no_v32 IN (SELECT booking_no FROM dbo.tblbookings b WHERE b.ContactID IN (SELECT id FROM @ContactIds));

DELETE it FROM dbo.tblitemtran it
WHERE EXISTS (
  SELECT 1 FROM dbo.tblbookings b
  WHERE b.ContactID IN (SELECT id FROM @ContactIds) AND it.booking_id = CAST(b.ID AS int));

DELETE FROM dbo.TblCrew
WHERE booking_no_v32 IN (SELECT booking_no FROM dbo.tblbookings WHERE ContactID IN (SELECT id FROM @ContactIds));

DELETE FROM dbo.tblbooknote
WHERE bookingNo IN (SELECT booking_no FROM dbo.tblbookings WHERE ContactID IN (SELECT id FROM @ContactIds));

DELETE FROM dbo.tblbookings WHERE ContactID IN (SELECT id FROM @ContactIds);
DELETE FROM dbo.tblLinkCustContact WHERE ContactID IN (SELECT id FROM @ContactIds);

DELETE c FROM dbo.tblcust c
WHERE c.ID IN (SELECT id FROM @OrgIds)
  AND NOT EXISTS (SELECT 1 FROM dbo.tblbookings b WHERE b.CustID = c.ID)
  AND NOT EXISTS (
    SELECT 1 FROM dbo.tblLinkCustContact l
    WHERE c.Customer_code IS NOT NULL AND l.Customer_Code IS NOT NULL AND l.Customer_Code = c.Customer_code
  )
  AND (
    c.iLink_ContactID IN (SELECT id FROM @ContactIds)
    OR c.iLink_ContactID IS NULL
    OR c.iLink_ContactID = 0
  );

DELETE FROM dbo.tblContact WHERE ID IN (SELECT id FROM @ContactIds);

IF OBJECT_ID('dbo.AgentThreads','U') IS NOT NULL
  DELETE FROM dbo.AgentThreads WHERE LOWER(LTRIM(UserKey)) = LOWER(@TargetEmail);

COMMIT TRANSACTION;
"

echo "--- IntentTestDB ($INTENT_DB): WestinLeads ---"
run_intent_sql "
SET NOCOUNT ON;
BEGIN TRANSACTION;
DELETE FROM dbo.WestinLeads WHERE LOWER(LTRIM(Email)) = LOWER(N'$EMAIL_SQL');
COMMIT TRANSACTION;
"

echo "--- IntentTestDB: AgentThreads (UserKey equals email) ---"
run_intent_sql "
SET NOCOUNT ON;
IF OBJECT_ID('dbo.AgentThreads','U') IS NOT NULL
BEGIN
  BEGIN TRANSACTION;
  DELETE FROM dbo.AgentThreads WHERE LOWER(LTRIM(UserKey)) = LOWER(N'$EMAIL_SQL');
  COMMIT TRANSACTION;
END
"

echo "=== Done ==="
