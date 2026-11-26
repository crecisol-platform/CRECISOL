#!/usr/bin/env python3
"""C06-11 independent final scope, prior evidence and visual-source guard.
This is not an automatic certificate; runtime gates are separate.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "076118ab5370e1fcbc7bfe0f1caefe07a664365b"
UI = "96174cf1d401b66464c02ba3e0d3a0cc2dd88cdf"
PATHS = frozenset([".github/workflows/c06-11-integral-certification.yml","docs/c06/C06_11_ESPECIFICACION.md","docs/c06/C06_11_MATRIZ_CIERRE.json","qa/c06_11_integral_audits.py"])
MATRIX = "docs/c06/C06_11_MATRIZ_CIERRE.json"

def need(ok, message):
    if not ok:
        raise AssertionError("C06-11 NO-GO: " + message)

def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()

def read(path):
    return (ROOT / path).read_text(encoding="utf-8")

def require_scope(paths):
    need(len(paths) == len(set(paths)) and frozenset(paths) == PATHS, "unexpected integral candidate scope")

def scope():
    need(subprocess.run(["git","merge-base","--is-ancestor",BASE,"HEAD"],cwd=ROOT).returncode==0,
         "ratified C06-10 base not ancestral")
    event = os.environ.get("GITHUB_EVENT_NAME","")
    if event == "pull_request":
        need(os.environ.get("GITHUB_BASE_REF") == "dev", "PR outside dev")
        require_scope(git("diff","--name-only","origin/dev...HEAD").splitlines())
    elif event == "push":
        need(os.environ.get("GITHUB_REF") == "refs/heads/dev", "push outside dev")
        require_scope(git("diff","--name-only","HEAD^","HEAD").splitlines())
    paths = git("diff","--name-only",BASE,"HEAD").splitlines()
    require_scope(paths)
    for mutant in (paths[:-1], paths + ["frontend/src/unreviewed.ts"], paths + ["backend/src/unreviewed.cs"],
                   paths + ["database/migrations/unreviewed.sql"], paths + ["docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"], paths + [paths[0]]):
        try:
            require_scope(mutant)
        except AssertionError:
            continue
        raise AssertionError("scope mutation accepted")
    print("PASS C06-11 exact four paths, six hostile scope mutations rejected")

def evidence():
    m = json.loads(read(MATRIX))
    need(m["schema_version"] == 1 and m["base_dev"] == BASE and m["status"] == "CANDIDATE_NOT_CERTIFIED"
         and m["certificate"] == "PENDING", "manifest scope or premature certificate")
    need(set(m["scope"]) == PATHS and len(m["scope"]) == 4, "manifest scope mismatch")
    acts = m["prior_acts"]
    need(len(acts) == 10 and [a["phase"] for a in acts] == ["C06-%02d" % i for i in range(1,11)]
         and len({a["path"] for a in acts}) == 10, "ten unique prior microphases required")
    for a in acts:
        need(a["path"].startswith("docs/c06/") and a["path"].endswith(".md"), "invalid act path")
        need(git("rev-parse",BASE+":"+a["path"]) == a["blob"] == git("rev-parse","HEAD:"+a["path"]),
             "prior act changed: "+a["phase"])
    r=m["c06_10_ratification"]
    need(r["act_sha"] == BASE and git("rev-parse",BASE+"^{tree}") == r["act_tree"]
         and git("rev-parse",r["technical_sha"]+"^{tree}") == r["technical_tree"]
         and git("rev-parse",r["act_pre_sha"]+"^{tree}") == r["act_tree"], "ratified tree provenance")
    need(r["act_post_run"] == 45740302813 and r["technical_post_run"] == 45737693483
         and r["technical_pre_run"] == 45734643721 and r["act_pre_run"] == 45737829949
         and r["technical_post_workflows"] == r["technical_pre_workflows"] == 15
         and r["act_pre_workflows"] == 7 and len(set(r["act_post_workflows"])) == 6
         and r["act_post_run"] in r["act_post_workflows"] and len(set(r["post_jobs"])) == 3
         and r["conclusion"] == "SUCCESS", "C06-10 recorded gate mismatch")
    from institutional_c06_04_act_audits import common
    common()
    print("PASS C06-11 ten immutable prior acts and recorded C06-10 ratification; live CI verified separately")

def inventory():
    from c06_10_offensive_audits import inventory as prior_inventory, harness, previous
    prior_inventory()
    harness()
    previous()
    roles=json.loads(read("docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json"))
    cat=json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json"))
    overlay=json.loads(read("docs/c06/C06_06_CATALOGO_ACTIVACION.json"))
    need(len(roles["roles"])==10 and len(roles["actions"])==108 and
         all(a["policy_activated"] is False and a["grantable"] is False for a in roles["actions"]), "role authority drift")
    future={a["action_id"] for a in cat["normative_actions"]}
    active=set(overlay["activated_normative"])
    need(len(future)==28 and active=={"platform.breakglass.open","platform.breakglass.read","platform.breakglass.close"}
         and active <= future and len(future-active)==25, "unreviewed future activation")
    need(all(a["grantable"] is False and a["http_registered"] is False for a in cat["normative_actions"]),
         "normative baseline activated")
    print("PASS C06-11 79 operations, 57 session, 22 proof, 228 negative cells, 10 roles/108 actions, 25 inactive future actions")

def visual_source():
    m=json.loads(read(MATRIX))["visual_baseline"]
    need(m["sha"]==UI and m["artifact"]==14862391857 and m["run"]==45265008041, "wrong visual provenance")
    for path, key in (("frontend/src","src_tree"),("frontend/package-lock.json","package_lock_blob")):
        need(git("rev-parse",UI+":"+path)==m[key]==git("rev-parse","HEAD:"+path), "visual source drift: "+path)
    changes=git("diff","--name-only",UI,"HEAD","--","frontend/").splitlines()
    need(all(p.startswith("frontend/tests/") for p in changes), "visual build input changed since approved F06")
    need(not git("diff","--name-only",BASE,"HEAD","--","frontend/","backend/","database/"),
         "product or test fixture changed in C06-11 candidate")
    import csv
    from c05_ux_f06_65_coverage import validate_commercial_ia_ratings
    with (ROOT/"docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv").open(encoding="utf-8",newline="") as f:
        buyer=validate_commercial_ia_ratings(list(csv.DictReader(f)))
    need(buyer["accepted"]==65 and buyer["minimum"]>=9.5 and buyer["runtimeAuthByImage"] is False,
         "prior individual visual grade evidence incomplete")
    print("PASS C06-11 immutable UI/build inputs and 65 prior grades; new original PNG comparison still required")

def main():
    stage=sys.argv[1] if len(sys.argv)==2 else ""
    need(stage in ("A","B","C","D","E"),"stage A/B/C/D/E")
    scope()
    if stage=="A":
        evidence()
        inventory()
    elif stage=="B":
        inventory()
    elif stage=="C":
        visual_source()
        inventory()
    elif stage=="D":
        need(len(list((ROOT/"database/tests").glob("c06_*_mysql.sh")))==7, "seven isolated MySQL suites required")
    else:
        visual_source()
    print("PASS C06-11 "+stage+" source gate; real runtime and editorial decision separately required")

if __name__=="__main__":
    main()
