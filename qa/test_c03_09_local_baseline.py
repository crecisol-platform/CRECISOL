#!/usr/bin/env python3
"""Positive/negative stdlib tests for C03-09 runner; do not test real toolchains."""
import contextlib
import io
import json
import os
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
from unittest import TestCase, main, mock

sys.path.insert(0, str(Path(__file__).parent))
import c03_09_local_baseline as baseline


class RunnerTests(TestCase):
    def test_strict_node_range(self):
        for v in ("v22.16.0", "v23.9.9", "v24.14.9", "v25.0.0", "v24.15", "24.15.0", ""):
            with self.subTest(v=v):
                self.assertFalse(baseline.is_node24(v))
        self.assertTrue(baseline.is_node24("v24.15.0"))
        self.assertTrue(baseline.is_node24("v24.99.1"))

    def test_requires_real_dotnet10_sdk(self):
        self.assertFalse(baseline.is_dotnet10("9.0.100 [/sdk]"))
        self.assertFalse(baseline.is_dotnet10("10.0-preview [/sdk]"))
        self.assertTrue(baseline.is_dotnet10("9.0.100 [/sdk]\n10.0.101 [/sdk]"))

    def test_rank_and_median(self):
        self.assertEqual(baseline.nearest_rank([3, 1, 2], 95), 3)
        self.assertEqual(baseline.summarize([3, 1, 2])["median_seconds"], 2)
        for values, p in (([], 95), ([1], 0), ([1], 101)):
            with self.subTest(values=values, p=p), self.assertRaises(ValueError):
                baseline.nearest_rank(values, p)

    def test_does_not_inherit_secrets_or_remote_docker_host(self):
        with mock.patch.dict(os.environ, {"CRECISOL_C01_READONLY_DSN": "secret",
                                               "DOCKER_HOST": "tcp://untrusted:2376",
                                               "NPM_TOKEN": "secret"}, clear=False):
            env = baseline.safe_env()
        self.assertNotIn("CRECISOL_C01_READONLY_DSN", env)
        self.assertNotIn("NPM_TOKEN", env)
        self.assertEqual(env["NPM_CONFIG_OFFLINE"], "true")
        if sys.platform.startswith("linux"):
            self.assertEqual(env["DOCKER_HOST"], "unix:///var/run/docker.sock")

    def test_explicit_local_ack_is_mandatory(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as cm:
            baseline.main(["--run"])
        self.assertEqual(cm.exception.code, 2)

    def test_project_versions_fail_closed_and_detect_changes(self):
        with TemporaryDirectory() as td:
            root = Path(td)
            self.assertFalse(baseline.project_versions(root))
            web = root / "frontend"
            api = root / "backend/src/Crecisol.Api"
            tests = root / "backend/tests/Crecisol.Outbox.ContractsTests"
            for folder in (web, api, tests):
                folder.mkdir(parents=True)
            pkg = {"dependencies": {"@angular/core": "22.2.4"}, "scripts": {
                "build": "ng build --configuration production",
                "prebuild": "python3 ../qa/c01_06_official_assets.py --write"}}
            (web / "package.json").write_text(json.dumps(pkg))
            for folder, fn in ((api, "Crecisol.Api.csproj"),
                               (tests, "Crecisol.Outbox.ContractsTests.csproj")):
                (folder / fn).write_text("<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")
            self.assertTrue(baseline.project_versions(root))
            pkg["dependencies"]["@angular/core"] = "23.0.0"
            (web / "package.json").write_text(json.dumps(pkg))
            self.assertFalse(baseline.project_versions(root))

    def test_blocked_probe_cannot_spawn_build_or_invent_metrics(self):
        with mock.patch.object(baseline, "probe", return_value={"ready": False, "checks": {}, "versions": {}}), \
             mock.patch.object(baseline, "run_fixed", side_effect=AssertionError("build MUST NOT run")):
            res = baseline.run_baseline(3)
        self.assertEqual(res["status"], "BLOCKED_MISSING_LOCAL_TOOLCHAIN")
        self.assertEqual(res["metrics"], {})
        self.assertFalse(res["real_compatibility_verified"])

    def test_failure_does_not_become_pass_and_logs_are_redacted(self):
        failure = mock.Mock(returncode=9, stdout=b"PRIVATE-STDOUT", stderr=b"PRIVATE-STDERR")
        with mock.patch.object(baseline.subprocess, "run", return_value=failure), \
             self.assertRaisesRegex(RuntimeError, r"exit=9") as cm:
            baseline.run_fixed("synthetic", ["true"], 5)
        self.assertNotIn("PRIVATE", str(cm.exception))
        with mock.patch.object(baseline, "probe", return_value={"ready": True}), \
             mock.patch.object(baseline, "run_fixed", side_effect=RuntimeError("synthetic failure")):
            res = baseline.run_baseline(3)
        self.assertEqual(res["status"], "FAILED_RUNTIME")
        self.assertFalse(res["real_compatibility_verified"])

    def test_selftest_does_not_create_ci_proof(self):
        with TemporaryDirectory() as td:
            with contextlib.redirect_stdout(io.StringIO()) as capture:
                ret = baseline.main(["--self-test"])
            self.assertEqual(ret, 0)
            self.assertIn("no benchmark fabricated", capture.getvalue())
            self.assertEqual(list(Path(td).iterdir()), [])


if __name__ == "__main__":
    main()
