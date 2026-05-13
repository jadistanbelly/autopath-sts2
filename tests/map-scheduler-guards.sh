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

rg -q "DisableCurrentRoomProceedButton" "$source_file" \
    || fail "auto-advance should disable the previous room proceed button before travel"

! rg -q "IsSinglePlayerOrFakeMultiplayer" "$source_file" \
    || fail "proceed-button cleanup must also run in multiplayer"

python3 - "$source_file" <<'PY' || fail "previous room proceed button must be disabled before map point selection"
import sys
source = open(sys.argv[1], encoding="utf-8").read()
disable = source.find("DisableCurrentRoomProceedButton();")
select = source.find("screen.OnMapPointSelectedLocally(target);")
if disable == -1 or select == -1 or disable > select:
    raise SystemExit(1)
PY

printf 'map scheduler guard checks passed\n'
