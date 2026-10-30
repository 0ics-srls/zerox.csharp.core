#!/usr/bin/env bash
# The Zerox.Ef tests on a real PostgreSQL, with progress [k/5]: a throwaway server in Docker, the three
# Ef test projects run against it (TestDatabase reads ZEROX_EF_TEST_POSTGRES and makes one database per
# test class), the server removed at the end. Without this script the Ef tests run on Sqlite in memory.
# Arguments go to each dotnet test: tools/test-postgres.sh --filter-class "*X*" runs only those tests.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
IMAGE="${ZEROX_EF_POSTGRES_IMAGE:-postgres:16-alpine}"
PORT="${ZEROX_EF_POSTGRES_PORT:-55432}"
NAME="zerox-ef-postgres-$$"

echo "[1/5] start $IMAGE on 127.0.0.1:$PORT"
docker run -d --rm --name "$NAME" -e POSTGRES_PASSWORD=zerox -p "127.0.0.1:$PORT:5432" "$IMAGE" >/dev/null
trap 'docker rm -f "$NAME" >/dev/null 2>&1 || true' EXIT

echo "[2/5] wait for the server"
for _ in $(seq 1 60); do
  if docker exec "$NAME" pg_isready -U postgres -h 127.0.0.1 >/dev/null 2>&1; then break; fi
  sleep 1
done
docker exec "$NAME" pg_isready -U postgres -h 127.0.0.1 >/dev/null

export ZEROX_EF_TEST_POSTGRES="Host=127.0.0.1;Port=$PORT;Username=postgres;Password=zerox"
k=3
for project in Db Domain Api; do
  echo "[$k/5] Zerox.Ef.$project.Tests on PostgreSQL"
  if [[ $# -gt 0 ]]; then
    # a focused run: a project with no test matching the filter is not a failure (exit code 8)
    dotnet test --project "tests/Zerox.Ef/$project/Zerox.Ef.$project.Tests.csproj" "$@" --ignore-exit-code 8
  else
    dotnet test --project "tests/Zerox.Ef/$project/Zerox.Ef.$project.Tests.csproj"
  fi
  k=$((k + 1))
done
