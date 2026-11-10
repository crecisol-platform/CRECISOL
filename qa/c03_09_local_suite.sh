#!/usr/bin/env bash
# C03-09 WSL2/Linux local suite. No Actions, installs, secrets or production.
set -uo pipefail
if [[ $# -ne 1 || ( "$1" != "--probe" && "$1" != "--run" ) ]]; then
  echo 'Usage: bash qa/c03_09_local_suite.sh --probe|--run' >&2; exit 2
fi
mode="$1"
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd -- "$root" || exit 2
if [[ "$(uname -s)" != "Linux" ]]; then
  echo 'BLOCKED: use Ubuntu/WSL2 or Linux' >&2; exit 2
fi
if ! command -v git >/dev/null || ! command -v python3 >/dev/null; then
  echo 'BLOCKED: git and Python 3 required' >&2; exit 2
fi
git_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
remote="$(git remote get-url origin 2>/dev/null || true)"
if [[ "$git_root" != "$root" || ! "$remote" =~ ^(git@github\.com:|https://github\.com/|ssh://git@github\.com/)solqaryn-platform/CRECISOL(\.git)?$ ]]; then
  echo 'BLOCKED: must run from a valid CRECISOL checkout' >&2; exit 2
fi
active_branch="$(git symbolic-ref --quiet --short HEAD 2>/dev/null || true)"
case "$active_branch" in
  "feature/c03-09-compat-benchmark-local"|"dev") : ;;
  *) echo 'BLOCKED: C03-09 tests may run only on its feature branch or authorized dev' >&2; exit 2 ;;
esac
if [[ -n "$(git status --porcelain --untracked-files=normal)" ]]; then
  echo 'BLOCKED: working tree not clean; preserve your local changes' >&2; exit 2
fi
sha="$(git rev-parse HEAD)"
case "$sha" in *[!a-f0-9]*|'') echo 'BLOCKED: invalid SHA' >&2; exit 2;; esac
if [[ ${#sha} -ne 40 ]]; then echo 'BLOCKED: invalid SHA length' >&2; exit 2; fi
umask 077
base="${CRECISOL_LOCAL_EVIDENCE_DIR:-$HOME/crecisol-local-evidence}"
mkdir -p -- "$base" || exit 2
base="$(cd -- "$base" && pwd -P)"
if [[ "$base" == "$root" || "$base" == "$root"/* ]]; then
  echo 'BLOCKED: evidence must be outside source tree' >&2; exit 2
fi
folder="$base/c03-09-$(date -u +%Y%m%dT%H%M%SZ)-${sha:0:12}"
mkdir -- "$folder" || exit 2
printf '%s\n' "C03-09 | $mode | commit $sha" > "$folder/source.txt"
run_check() {
  local label="$1"; shift
  if "$@" > "$folder/$label.txt" 2>&1; then
    echo "PASS $label"; return 0
  fi
  echo "FAIL $label: inspect $folder/$label.txt" >&2; return 1
}
a="PASS"; b="PASS"; c="NOT_EXECUTED"
run_check 'A_master_integrity' python3 qa/master_source_guard.py --self-test || a="FAIL"
run_check 'A_historical_static' python3 qa/c03_local_preflight.py || a="FAIL"
run_check 'A_c03_07_source' python3 qa/c03_07_outbox_audits.py A || a="FAIL"
run_check 'A_c03_08_source' python3 qa/c03_08_local_config_audits.py A || a="FAIL"
run_check 'B_negative_unittests' python3 -m unittest discover -s qa -p test_c03_09_local_baseline.py -v || b="FAIL"
run_check 'B_orchestrator_contracts' python3 -m unittest discover -s qa -p test_c03_09_local_suite_contract.py -v || b="FAIL"
run_check 'B_c03_07_negative' python3 qa/c03_07_outbox_audits.py B || b="FAIL"
run_check 'B_c03_08_negative' python3 qa/c03_08_local_config_audits.py B || b="FAIL"
run_check 'B_guard_selftest' python3 qa/c03_09_local_baseline.py --self-test || b="FAIL"
probe_ok=0
if python3 qa/c03_09_local_baseline.py --probe > "$folder/probe.json" 2> "$folder/probe_error.txt"; then
  probe_ok=1
  echo 'READY: runtime toolchain probe'
else
  echo 'BLOCKED: inspect probe.json for missing toolchains'
fi
if [[ "$mode" == "--run" ]]; then
  if [[ "$a" != "PASS" || "$b" != "PASS" ]]; then
    c="BLOCKED_A_OR_B"
  elif python3 - "$folder/probe.json" <<'CHECK_READY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as f: p=json.load(f)
sys.exit(0 if p.get('status') == 'READY' and p.get('environment', {}).get('ready') else 2)
CHECK_READY
  then
    if python3 qa/c03_09_local_baseline.py --run --confirm-local > "$folder/C_runtime_benchmark.json" 2> "$folder/C_runtime_error.txt" && \
       python3 - "$folder/C_runtime_benchmark.json" <<'CHECK_C'
import json, sys
with open(sys.argv[1], encoding='utf-8') as f: r=json.load(f)
expected={'dotnet_api_release_incremental_build', 'angular22_production_incremental_build',
          'dotnet_outbox_contract_build', 'mysql84_disposable_outbox_smoke'}
ok=(r.get('status') == 'MEASURED_LOCAL_ONLY_NOT_CERTIFIED'
    and r.get('real_compatibility_verified') is True
    and expected.issubset(r.get('metrics', {}))
    and all(len(r['metrics'][k]['samples_seconds']) >= 3
            for k in expected if k != 'mysql84_disposable_outbox_smoke'))
sys.exit(0 if ok else 2)
CHECK_C
    then
      c="EXECUTED"
    else
      c="FAILED_RUNTIME"
    fi
  else
    c="BLOCKED_MISSING_LOCAL_TOOLCHAIN"
  fi
fi
python3 - "$folder" "$sha" "$mode" "$a" "$b" "$c" <<'REPORT'
from pathlib import Path
from hashlib import sha256
import datetime, json, sys
folder, sha, mode, a, b, c = sys.argv[1:]
folder = Path(folder)
files = {f.name: sha256(f.read_bytes()).hexdigest() for f in sorted(folder.iterdir()) if f.is_file()}
result = {
  'project': 'solqaryn-platform/CRECISOL',
  'phase': 'C03-09',
  'commit': sha,
  'execution_mode': mode,
  'time_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
  'audit_A_static_traceability': a,
  'audit_B_negative_guards': b,
  'audit_C_real_compatibility': c,
  'ci_postmerge': 'NOT_CLAIMED',
  'phase_certified': False,
  'file_sha256': files,
}
(folder / 'manifest.json').write_text(json.dumps(result, sort_keys=True, indent=2) + '\n', encoding='utf-8')
print('EVIDENCE:', folder)
print('RESULT:', json.dumps({k: result[k] for k in (
  'audit_A_static_traceability', 'audit_B_negative_guards',
  'audit_C_real_compatibility', 'phase_certified')}))
REPORT
if [[ "$a" != "PASS" || "$b" != "PASS" ]]; then exit 1; fi
if [[ "$mode" == "--run" && "$c" != "EXECUTED" ]]; then exit 2; fi
if [[ "$mode" == "--probe" && "$probe_ok" != "1" ]]; then exit 2; fi
exit 0
