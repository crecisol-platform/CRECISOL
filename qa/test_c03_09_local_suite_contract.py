#!/usr/bin/env python3
"""Synthetic contract tests of the C03-09 shell orchestrator (not product tests)."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory
from unittest import TestCase, main, skipUnless

ROOT = Path(__file__).resolve().parent
SOURCE_SHELL = ROOT / 'c03_09_local_suite.sh'


@skipUnless(sys.platform.startswith('linux') and shutil.which('git') and shutil.which('bash'),
            'C03-09 suite requires bash/git on Linux/WSL2')
class LocalSuiteContractTests(TestCase):
    def setUp(self):
        self.tmp = TemporaryDirectory(prefix='crecisol-c03-09-fixture-')
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.checkout = self.root / 'checkout'
        qa = self.checkout / 'qa'
        qa.mkdir(parents=True)
        shutil.copyfile(SOURCE_SHELL, qa / 'c03_09_local_suite.sh')
        self.evidence = self.root / 'evidence'
        (self.checkout / '.gitignore').write_text('__pycache__/\n*.pyc\n', encoding='utf-8')
        for name in ('master_source_guard.py', 'c03_local_preflight.py'):
            (qa / name).write_text("print('SYNTHETIC static fixture only')\n", encoding='utf-8')
        source = """import os,sys
if len(sys.argv) != 2 or sys.argv[1] not in ('A','B'):
    raise SystemExit(2)
if os.environ.get('SIMULATE_B_FAILURE') == '1' and sys.argv[1] == 'B' and __file__.endswith('c03_08_local_config_audits.py'):
    raise SystemExit(7)
print('SYNTHETIC old chapter',sys.argv[1])
"""
        for name in ('c03_07_outbox_audits.py', 'c03_08_local_config_audits.py'):
            (qa / name).write_text(source, encoding='utf-8')
        (qa / 'test_c03_09_local_baseline.py').write_text(
            'import unittest\nclass FixtureTest(unittest.TestCase):\n    def test_ok(self): self.assertTrue(True)\n', encoding='utf-8')
        (qa / 'test_c03_09_local_suite_contract.py').write_text(
            'import unittest\nclass FixtureShellTest(unittest.TestCase):\n    def test_ok(self): self.assertTrue(True)\n', encoding='utf-8')
        (qa / 'test_c03_09_dev_db_tls_probe.py').write_text(
            'import unittest\nclass FixtureDatabaseSafetyTest(unittest.TestCase):\n    def test_ok(self): self.assertTrue(True)\n', encoding='utf-8')
        (qa / 'test_c03_09_word_logo_compat.py').write_text(
            'import unittest\nclass FixtureCurrentWordTest(unittest.TestCase):\n    def test_ok(self): self.assertTrue(True)\n', encoding='utf-8')
        (qa / 'c03_09_local_baseline.py').write_text("""import json,sys
if '--self-test' in sys.argv:
    print('SYNTHETIC guard test')
    raise SystemExit(0)
if '--probe' in sys.argv:
    print(json.dumps({'status':'BLOCKED_MISSING_LOCAL_TOOLCHAIN','environment': {'ready': False},'metrics': {},'real_compatibility_verified': False}))
    raise SystemExit(2)
raise RuntimeError('A synthetic fixture must never run real builds')
""", encoding='utf-8')
        self.git('init', '-q', '-b', 'feature/c03-09-compat-benchmark-local')
        self.git('config', 'user.name', 'SyntheticFixture')
        self.git('config', 'user.email', 'fixture@example.test')
        self.git('remote', 'add', 'origin', 'https://github.com/solqaryn-platform/CRECISOL.git')
        self.git('add', '.')
        self.git('-c', 'core.hooksPath=/dev/null', 'commit', '-qm', 'synthetic-only')

    def git(self, *args):
        subprocess.run(['git', *args], cwd=self.checkout, check=True, stdout=subprocess.DEVNULL,
                       stderr=subprocess.PIPE, timeout=5)

    def run_suite(self, mode='--probe', **envvars):
        env = dict(os.environ)
        env['CRECISOL_LOCAL_EVIDENCE_DIR'] = str(self.evidence)
        env.update(envvars)
        return subprocess.run(['bash', str(self.checkout / 'qa/c03_09_local_suite.sh'), mode],
                              cwd=self.checkout, env=env, capture_output=True,
                              text=True, check=False, timeout=18)

    def evidence_manifest(self):
        candidates = sorted(self.evidence.glob('c03-*/manifest.json'))
        self.assertEqual(len(candidates), 1, f'Exactly one manifest expected: {candidates}')
        return json.loads(candidates[0].read_text(encoding='utf-8'))

    def test_reject_main_before_running(self):
        self.git('switch', '-q', '-c', 'main')
        result = self.run_suite()
        self.assertEqual(result.returncode, 2)
        self.assertIn('C03-09 tests may run only', result.stderr)
        self.assertFalse(self.evidence.exists())

    def test_reject_unapproved_remote_origin(self):
        self.git('remote', 'set-url', 'origin', 'https://github.com/someone/other.git')
        result = self.run_suite()
        self.assertEqual(result.returncode, 2)
        self.assertIn('valid CRECISOL checkout', result.stderr)
        self.assertFalse(self.evidence.exists())

    def test_probe_runs_eleven_static_checks_without_certifying(self):
        result = self.run_suite()
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertEqual(result.stdout.count('PASS '), 11)
        manifest = self.evidence_manifest()
        self.assertEqual(manifest['audit_A_static_traceability'], 'PASS')
        self.assertEqual(manifest['audit_B_negative_guards'], 'PASS')
        self.assertEqual(manifest['audit_C_real_compatibility'], 'NOT_EXECUTED')
        self.assertFalse(manifest['phase_certified'])
        self.assertTrue({'A_c03_07_source.txt','A_c03_08_source.txt','B_c03_07_negative.txt','B_c03_08_negative.txt','B_orchestrator_contracts.txt','B_dev_db_tls_safety.txt','B_word_logo_current_edition.txt'}.issubset(manifest['file_sha256']))

    def test_run_without_toolchains_remains_blocked(self):
        result = self.run_suite('--run')
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        manifest = self.evidence_manifest()
        self.assertEqual(manifest['audit_C_real_compatibility'], 'BLOCKED_MISSING_LOCAL_TOOLCHAIN')
        self.assertFalse(manifest['phase_certified'])
        self.assertNotIn('C_runtime_benchmark.json', manifest['file_sha256'])

    def test_failed_security_regression_blocks_c(self):
        result = self.run_suite('--run', SIMULATE_B_FAILURE='1')
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        manifest = self.evidence_manifest()
        self.assertEqual(manifest['audit_A_static_traceability'], 'PASS')
        self.assertEqual(manifest['audit_B_negative_guards'], 'FAIL')
        self.assertEqual(manifest['audit_C_real_compatibility'], 'BLOCKED_A_OR_B')
        self.assertFalse(manifest['phase_certified'])



class WindowsLauncherStaticTests(TestCase):
    """Source-only guardrails: PowerShell/WSL execution remains an external test."""

    @classmethod
    def setUpClass(cls):
        cls.script = (ROOT / 'c03_09_windows.ps1').read_text(encoding='utf-8')

    def test_never_depends_on_wslpath_conversion(self):
        self.assertNotIn('wslpath -a', self.script)
        self.assertNotIn('--exec wslpath', self.script)
        self.assertIn('("--cd", $repo, "--exec", "bash", "qa/c03_09_local_suite.sh", $option)', self.script)

    def test_supports_wsl_unc_without_invoking_shell_interpolation(self):
        self.assertIn('$linuxRelative = $Matches[2].Replace(', self.script)
        self.assertIn('"--distribution", $distro, "--cd", $linuxRepo', self.script)
        self.assertNotIn('Invoke-Expression', self.script)
        self.assertNotIn('Start-Process', self.script)

    def test_only_probe_or_run_and_propagates_runner_status(self):
        self.assertIn('[ValidateSet("Probe", "Run")]', self.script)
        self.assertIn('if ($Mode -eq "Run") { "--run" } else { "--probe" }', self.script)
        self.assertIn('exit ([int]$exitCode)', self.script)
        self.assertIn('Stop-LocalLauncher', self.script)

    def test_launcher_has_no_implicit_install_or_remote_db_contact(self):
        for disallowed in ('winget install', 'choco install', 'Invoke-WebRequest',
                           'docker pull', 'npm install', 'dotnet restore',
                           'mysql --host', 'crecisol_dev_readonly'):
            with self.subTest(disallowed=disallowed):
                self.assertNotIn(disallowed, self.script)

if __name__ == '__main__':
    main()
