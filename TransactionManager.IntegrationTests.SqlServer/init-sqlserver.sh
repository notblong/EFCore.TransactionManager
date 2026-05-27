#!/usr/bin/env bash
set -euo pipefail

server="sqlserver"
user="sa"
password="Your_password123"
database="TransactionManagerIntegrationTests"
sqlcmd="/opt/mssql-tools/bin/sqlcmd"

for i in {1..60}; do
  if "$sqlcmd" -S "$server" -U "$user" -P "$password" -Q "SELECT 1"; then
    break
  fi

  if [ "$i" = "60" ]; then
    echo "SQL Server did not become ready in time."
    exit 1
  fi

  sleep 2
done

"$sqlcmd" -S "$server" -U "$user" -P "$password" \
  -Q "IF DB_ID(N'$database') IS NULL CREATE DATABASE [$database]"

if "$sqlcmd" -S "$server" -U "$user" -P "$password" \
  -d "$database" \
  -Q "IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL THROW 51000, 'Schema missing.', 1"; then
  echo "SQL Server schema already exists."
  exit 0
fi

"$sqlcmd" -S "$server" -U "$user" -P "$password" \
  -d "$database" \
  -i /schema.sql
