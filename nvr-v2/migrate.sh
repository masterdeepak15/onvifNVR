#!/bin/bash
# EF Core Database Migration Helper
# Usage: ./migrate.sh [add|update|list] [migration-name]

set -e

PROJECT_DIR="src/NVR.Infrastructure"
STARTUP_PROJECT="src/NVR.API"

case "$1" in
  "add")
    if [ -z "$2" ]; then
      echo "Usage: ./migrate.sh add <MigrationName>"
      exit 1
    fi
    echo "📦 Adding migration: $2"
    dotnet ef migrations add "$2" \
      --project "$PROJECT_DIR" \
      --startup-project "$STARTUP_PROJECT" \
      --output-dir "Migrations"
    ;;
  
  "update")
    echo "🚀 Applying migrations..."
    dotnet ef database update \
      --project "$PROJECT_DIR" \
      --startup-project "$STARTUP_PROJECT"
    ;;
  
  "list")
    echo "📋 Listing migrations..."
    dotnet ef migrations list \
      --project "$PROJECT_DIR" \
      --startup-project "$STARTUP_PROJECT"
    ;;
  
  "reset")
    echo "⚠️  Resetting database..."
    dotnet ef database drop --force \
      --project "$PROJECT_DIR" \
      --startup-project "$STARTUP_PROJECT"
    dotnet ef database update \
      --project "$PROJECT_DIR" \
      --startup-project "$STARTUP_PROJECT"
    ;;
  
  "script")
    echo "📄 Generating SQL script..."
    dotnet ef migrations script \
      --project "$PROJECT_DIR" \
      --startup-project "$STARTUP_PROJECT" \
      --output migrations.sql \
      --idempotent
    echo "SQL script saved to migrations.sql"
    ;;
  
  *)
    echo "NVR Database Migration Tool"
    echo ""
    echo "Commands:"
    echo "  add <name>  - Add a new migration"
    echo "  update      - Apply pending migrations"
    echo "  list        - List all migrations"
    echo "  reset       - Drop and recreate database"
    echo "  script      - Generate idempotent SQL script"
    ;;
esac
