#!/bin/bash
# Post-deploy smoke test. Verifies the deployed environment is functional.
# Called by deploy.yml after the health check, or manually:
#   UAT_URL="https://your-app.example" bash scripts/smoke-test-uat.sh
#
# Exit codes: 0 = all checks passed, 1 = one or more checks failed

set -euo pipefail

UAT="${UAT_URL:-https://your-app.example}"   # TODO: default to your UAT URL, or always pass UAT_URL
PASS=0
FAIL=0

check() {
  if [ "$2" = "true" ]; then echo "  PASS: $1"; PASS=$((PASS + 1)); else echo "  FAIL: $1"; FAIL=$((FAIL + 1)); fi
}

echo "=== Smoke Test ==="
echo "Target: $UAT"
echo ""

# 1. Health check
HTTP=$(curl -s -o /dev/null -w "%{http_code}" "$UAT/healthz" --max-time 60)
check "Health check (/healthz)" "$([ "$HTTP" = "200" ] && echo true || echo false)"

# 2. Pricing page loads
HTTP=$(curl -s -o /dev/null -w "%{http_code}" "$UAT/pricing" --max-time 15)
check "Pricing page (/pricing)" "$([ "$HTTP" = "200" ] && echo true || echo false)"

# 3. Login page loads
HTTP=$(curl -s -o /dev/null -w "%{http_code}" "$UAT/login" --max-time 15)
check "Login page (/login)" "$([ "$HTTP" = "200" ] && echo true || echo false)"

# 4. Auth magic-link endpoint exists (200 sent or 429 rate-limited — not 404/500)
HTTP=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$UAT/api/auth/magic-link" \
  -H "Content-Type: application/json" \
  -d '{"email": "smoke-test@example.com"}' \
  --max-time 15)
check "Auth endpoint exists" "$([ "$HTTP" = "200" ] || [ "$HTTP" = "429" ] && echo true || echo false)"

echo ""
echo "=== Results: $PASS passed, $FAIL failed ==="
[ "$FAIL" -gt 0 ] && { echo "SMOKE TEST FAILED"; exit 1; }
echo "SMOKE TEST PASSED"
exit 0
