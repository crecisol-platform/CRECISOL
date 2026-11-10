#!/usr/bin/env python3
"""C03-09: offline, fail-closed compatibility probe and local baseline runner.

No GitHub Actions, production data, remote service, new deployments or payments.
A benchmark PASS requires real Node 24, .NET 10 and MySQL 8.4, not simulations.
"""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
import os
import platform
from pathlib import Path
import re
import shutil
import signal
import tempfile
import statistics
import subprocess
import xml.etree.ElementTree as ET
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
MYSQL_IMAGE = "mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"


def safe_env():
    keys = ("PATH", "HOME", "TMPDIR", "LANG", "LC_ALL", "SYSTEMROOT", "COMSPEC")
    env = {k: os.environ[k] for k in keys if k in os.environ}
    env.update(DOTNET_NOLOGO="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
               DOTNET_CLI_TELEMETRY_OPTOUT="1", NPM_CONFIG_OFFLINE="true",
               NPM_CONFIG_AUDIT="false", CI="true")
    if sys.platform.startswith("linux"):
        env["DOCKER_HOST"] = "unix:///var/run/docker.sock"
    return env


def version(args, timeout=8):
    if not shutil.which(args[0]):
        return ""
    try:
        p = subprocess.run(args, cwd=ROOT, env=safe_env(), capture_output=True,
                           text=True, timeout=timeout, check=False)
        return p.stdout.strip() if p.returncode == 0 else ""
    except (OSError, subprocess.TimeoutExpired):
        return ""


def is_node24(raw):
    m = re.fullmatch(r"v(\d+)\.(\d+)\.\d+", raw.strip())
    return bool(m and int(m[1]) == 24 and int(m[2]) >= 15)


def is_dotnet10(raw):
    return any(re.match(r"^10\.0\.\d+\s+\[", line) for line in raw.splitlines())


def nearest_rank(samples, percentile):
    if not samples or not 0 < percentile <= 100:
        raise ValueError("non-empty samples and percentile in (0,100] required")
    import math
    ordered = sorted(samples)
    return ordered[max(0, math.ceil(len(ordered) * percentile / 100) - 1)]


def project_versions(root):
    try:
        pkg = json.loads((root / "frontend/package.json").read_text(encoding="utf-8"))
        api = ET.parse(root / "backend/src/Crecisol.Api/Crecisol.Api.csproj")
        test = ET.parse(root / "backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj")
        angular = pkg.get("dependencies", {}).get("@angular/core", "")
        scripts = pkg.get("scripts", {})
        return (re.fullmatch(r"22\.\d+\.\d+", angular) is not None
                and scripts.get("build") == "ng build --configuration production"
                and scripts.get("prebuild") == "python3 ../qa/c01_06_official_assets.py --write"
                and api.getroot().findtext(".//TargetFramework") == "net10.0"
                and test.getroot().findtext(".//TargetFramework") == "net10.0")
    except (OSError, ValueError, ET.ParseError):
        return False


def probe(root=ROOT):
    node = version(["node", "--version"])
    dotnet = version(["dotnet", "--list-sdks"])
    npm = version(["npm", "--version"])
    docker = version(["docker", "--version"])
    checks = {
        "node_24_15_plus": is_node24(node),
        "dotnet_sdk_10": is_dotnet10(dotnet),
        "npm_available": bool(npm),
        "frontend_dependencies_cached": (root / "frontend/node_modules/.bin/ng").is_file(),
        "dotnet_api_assets_cached": (root / "backend/src/Crecisol.Api/obj/project.assets.json").is_file(),
        "dotnet_outbox_assets_cached": (root / "backend/tests/Crecisol.Outbox.ContractsTests/obj/project.assets.json").is_file(),
        "package_and_framework_versions_pinned": project_versions(root),
        "bash_available": bool(shutil.which("bash")),
        "project_sources_present": all((root / p).is_file() for p in (
            "frontend/package.json", "frontend/angular.json",
            "backend/src/Crecisol.Api/Crecisol.Api.csproj",
            "backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj",
            "database/tests/c03_07_outbox_mysql_smoke.sh")),
        "docker_local_linux": sys.platform.startswith("linux") and bool(docker)
                              and Path("/var/run/docker.sock").exists(),
    }
    checks["mysql_84_image_cached"] = bool(checks["docker_local_linux"] and version(
        ["docker", "image", "inspect", MYSQL_IMAGE, "--format", "{{.Id}}"], timeout=12))
    checks["docker_local_engine_ready"] = bool(checks["docker_local_linux"] and version(
        ["docker", "info", "--format", "{{.ServerVersion}}"], timeout=12))
    return {
        "ready": all(checks.values()), "checks": checks,
        "versions": {"node": node, "npm": npm, "dotnet_sdks": dotnet.splitlines(), "docker": docker},
    }


def run_fixed(label, args, timeout):
    """Capture exit status without keeping build logs or child processes alive.

    The only supported runtime is local Linux/WSL2. Child process groups are
    explicitly killed on timeout; stderr is hashed, not printed or persisted.
    """
    if not args or args[0] not in ("dotnet", "npm", "bash"):
        raise RuntimeError(f"{label} uses an unapproved command")
    start = time.perf_counter()
    with tempfile.TemporaryFile() as errors:
        try:
            child = subprocess.Popen(
                args, cwd=ROOT, env=safe_env(), stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL, stderr=errors, start_new_session=True)
        except OSError as exc:
            raise RuntimeError(f"{label} could not start ({type(exc).__name__})") from exc
        try:
            exit_code = child.wait(timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt) as exc:
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            finally:
                if child.poll() is None:
                    child.kill()
                child.wait()
            if isinstance(exc, KeyboardInterrupt):
                raise
            raise RuntimeError(f"{label} timeout after {timeout}s") from exc
        elapsed = round(time.perf_counter() - start, 4)
        if exit_code != 0:
            errors.seek(0)
            digest = sha256()
            for block in iter(lambda: errors.read(65536), b""):
                digest.update(block)
            raise RuntimeError(
                f"{label} failed (exit={exit_code}, stderr_sha256={digest.hexdigest()})")
    return elapsed


def summarize(samples):
    return {"samples_seconds": samples, "median_seconds": statistics.median(samples),
            "p95_nearest_rank_seconds": nearest_rank(samples, 95),
            "min_seconds": min(samples), "max_seconds": max(samples)}


def run_baseline(rounds):
    environment = probe()
    if not environment["ready"]:
        return {"status": "BLOCKED_MISSING_LOCAL_TOOLCHAIN", "environment": environment,
                "metrics": {}, "real_compatibility_verified": False}
    commands = {
        "dotnet_api_release_incremental_build": (["dotnet", "build", "backend/src/Crecisol.Api/Crecisol.Api.csproj",
                                                  "-c", "Release", "--no-restore", "-warnaserror"], 300),
        "angular22_production_incremental_build": (["npm", "run", "build", "--prefix", "frontend"], 360),
        "dotnet_outbox_contract_build": (["dotnet", "build", "backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj",
                                           "-c", "Release", "--no-restore", "-warnaserror"], 300),
    }
    metrics = {}
    try:
        for label, (cmd, timeout) in commands.items():
            samples = [run_fixed(label, cmd, timeout) for _ in range(rounds)]
            metrics[label] = summarize(samples)
        # This smoke uses a disposable MySQL 8.4 container with --network none.
        # It is an integration duration, not a claim about SQL query latency.
        smoke = run_fixed("mysql84_disposable_outbox_smoke",
                          ["bash", "database/tests/c03_07_outbox_mysql_smoke.sh"], 480)
        metrics["mysql84_disposable_outbox_smoke"] = {"duration_seconds": smoke,
            "kind": "integration_duration_not_query_latency"}
    except RuntimeError as exc:
        return {"status": "FAILED_RUNTIME", "environment": environment,
                "metrics": metrics, "error": str(exc), "real_compatibility_verified": False}
    return {"status": "MEASURED_LOCAL_ONLY_NOT_CERTIFIED", "environment": environment,
            "metrics": metrics, "real_compatibility_verified": True}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--probe", action="store_true")
    group.add_argument("--run", action="store_true")
    group.add_argument("--self-test", action="store_true")
    parser.add_argument("--rounds", type=int, default=3, choices=(3, 4, 5))
    parser.add_argument("--confirm-local", action="store_true",
                        help="Allow only local cached builds and disposable --network none MySQL")
    args = parser.parse_args(argv)
    if args.self_test:
        assert is_node24("v24.15.0") and is_node24("v24.99.1")
        assert not is_node24("v22.16.0") and not is_node24("v25.0.0")
        assert is_dotnet10("9.0.100 [/sdk]\n10.0.100 [/sdk]")
        assert not is_dotnet10("9.0.100 [/sdk]")
        assert nearest_rank([2, 1, 3], 95) == 3
        assert summarize([2, 1, 3])["median_seconds"] == 2
        assert isinstance(probe()["ready"], bool)
        print("PASS C03-09 self-test: version guards, percentile and fail-closed probe; no benchmark fabricated")
        return 0
    if args.run and not args.confirm_local:
        parser.error("--run requires --confirm-local; no remote services allowed")
    result = run_baseline(args.rounds) if args.run else {
        "status": "READY" if (p := probe())["ready"] else "BLOCKED_MISSING_LOCAL_TOOLCHAIN",
        "environment": p, "metrics": {}, "real_compatibility_verified": False,
    }
    result["generated_utc"] = datetime.now(timezone.utc).isoformat()
    result["machine_class"] = {"os": platform.system(), "arch": platform.machine(),
                                "cpu_count": os.cpu_count(), "python": platform.python_version()}
    head = version(["git", "rev-parse", "HEAD"])
    result["git_commit"] = head if re.fullmatch(r"[a-f0-9]{40}", head) else None
    result["scope"] = "C03-09/LOCAL_ONLY/NO_CI_NO_CERTIFICATION"
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0 if result["status"] in ("READY", "MEASURED_LOCAL_ONLY_NOT_CERTIFIED") else 2


if __name__ == "__main__":
    sys.exit(main())
