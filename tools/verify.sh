#!/usr/bin/env bash
# Every check of the repository, in order, with progress [k/4]; stops at the first that fails.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
TOTAL=4

step() {   # step <k> <name> <function>
  echo "[$1/$TOTAL] $2"
  if "$3"; then
    echo "[$1/$TOTAL] ok"
  else
    echo "[$1/$TOTAL] FAILED"
    exit 1
  fi
}

build() { dotnet build zerox.csharp.core.slnx -warnaserror; }
format() { dotnet format zerox.csharp.core.slnx --verify-no-changes; }
tests() { dotnet test --solution zerox.csharp.core.slnx; }
# exit 3 = ast-grep not installed: a warning, not a failure
astgrep() {
  local code=0
  bash .claude/support/lint/lint-run.sh || code=$?
  if [[ $code -eq 3 ]]; then echo "ast-grep not installed: layer skipped"; return 0; fi
  return $code
}

step 1 build build
step 2 format format
step 3 tests tests
step 4 ast-grep astgrep
