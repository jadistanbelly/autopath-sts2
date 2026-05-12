#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$repo_root/Patches/MapScreenPatch.cs"

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

rg -q "GetCurrentMapLocation" "$source_file" \
    || fail "scheduler must capture the current map location when scheduling"

rg -q "readonly record struct PendingAutoAdvance" "$source_file" \
    || fail "scheduler state should be represented by a PendingAutoAdvance record"

rg -q "OnTimerFired\\(pending\\)" "$source_file" \
    || fail "timer callback must carry one pending auto-advance value"

rg -q "IsPendingCurrent\\(pending\\)" "$source_file" \
    || fail "timer must reject stale map locations before auto-advancing"

rg -q "CancelPending\\(__instance\\)" "$source_file" \
    || fail "map close must cancel pending auto-advance timers"

printf 'map scheduler guard checks passed\n'
