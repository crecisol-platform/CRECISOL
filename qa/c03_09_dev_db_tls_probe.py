#!/usr/bin/env python3
"""Opt-in, read-only MySQL 8.4 DEV identity/TLS probe for CRECISOL C03-09.

Never connects on import, --plan or --self-test.  No schema or data mutations.
Credentials remain in the operator's private MySQL login path, outside Git.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

PROJECT_ROOT = Path(__file__).resolve().parents[1]
DATABASE = "crecisol_dev"
LOGIN_PATH = "crecisol_dev_readonly"
FIXED_SQL = "SELECT VERSION(), DATABASE(); SHOW SESSION STATUS LIKE 'Ssl_cipher';"


def safe_env():
    """Do not pass MYSQL_PWD, application DSNs, proxy secrets or test credentials."""
    allowed = ("PATH", "HOME", "SYSTEMROOT", "LANG", "LC_ALL")
    return {key: os.environ[key] for key in allowed if key in os.environ}


def validate_ca(raw):
    """Only trust an explicit, regular PEM certificate OUTSIDE the Git checkout."""
    if not raw:
        raise ValueError("trusted server CA path required")
    path = Path(raw).expanduser()
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        raise ValueError("trusted server CA must be a regular absolute file, not a symlink")
    resolved = path.resolve()
    if resolved == PROJECT_ROOT or PROJECT_ROOT in resolved.parents:
        raise ValueError("trusted CA must stay outside the CRECISOL repository")
    if path.stat().st_size < 40 or path.stat().st_size > 2_000_000:
        raise ValueError("trusted CA file size is invalid")
    if b"-----BEGIN CERTIFICATE-----" not in path.read_bytes()[:4096]:
        raise ValueError("trusted CA must be a PEM certificate")
    return resolved


def mysql_command(client, ca):
    """Fixed arguments and SQL; never interpolate host, password or user inputs."""
    return [client, "--no-defaults", f"--login-path={LOGIN_PATH}",
            "--protocol=TCP", "--ssl-mode=VERIFY_IDENTITY", f"--ssl-ca={ca}",
            f"--database={DATABASE}", "--connect-timeout=5", "--skip-reconnect",
            "--batch", "--skip-column-names", "--raw", "--skip-auto-rehash",
            "--local-infile=0", f"--execute={FIXED_SQL}"]


def parse_mysql_response(raw):
    """Accept only the expected two read-only result sets and a real TLS cipher."""
    lines = raw.strip().splitlines()
    if len(lines) != 2:
        raise ValueError("unexpected read-only metadata response")
    version_row = lines[0].split("\t")
    cipher_row = lines[1].split("\t")
    if len(version_row) != 2 or len(cipher_row) != 2:
        raise ValueError("unexpected read-only metadata column count")
    version, db = version_row
    key, cipher = cipher_row
    if not re.fullmatch(r"8\.4\.\d+(?:[-+][\w.\-]+)?", version):
        raise ValueError("remote DEV server is not verified MySQL 8.4")
    if db != DATABASE:
        raise ValueError("remote database does not match CRECISOL DEV")
    if key != "Ssl_cipher" or not cipher or cipher.upper() in ("NULL", "NONE", "DISABLED"):
        raise ValueError("remote TLS cipher was not established")
    if not re.fullmatch(r"[A-Za-z0-9_-]{3,90}", cipher):
        raise ValueError("invalid remote cipher metadata")
    return {"mysql_server_version": version, "database_matches": True,
            "tls_cipher_present": True, "ca_and_identity_verification_requested": True,
            "read_only_metadata_checks": 2}


def connect_readonly(ca_path):
    ca = validate_ca(ca_path)
    client = shutil.which("mysql")
    if not client:
        raise ValueError("MySQL CLI 8.4 is not installed on this machine")
    args = mysql_command(client, ca)
    try:
        result = subprocess.run(args, cwd=PROJECT_ROOT, env=safe_env(),
                                stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, timeout=15, check=False,
                                text=True)
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise ValueError("DEV database connection failed before trusted verification") from exc
    if result.returncode != 0:
        # Not even stderr hashes: MySQL diagnostics can disclose the hostname/account.
        raise ValueError("DEV connection or TLS identity verification failed")
    return parse_mysql_response(result.stdout)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--plan", action="store_true", help="Display safety plan; no connection")
    group.add_argument("--self-test", action="store_true", help="Test parser; no connection")
    group.add_argument("--connect-readonly", action="store_true", help="Explicit external DEV probe")
    parser.add_argument("--confirm-dev-only", action="store_true",
                        help="Explicitly confirm this is CRECISOL DEV, never production data")
    parser.add_argument("--ca", help="Trusted server CA PEM outside the repository")
    options = parser.parse_args(argv)
    response = {"scope": "C03-09/CRECISOL_DEV/READ_ONLY_OPT_IN",
                "generated_utc": datetime.now(timezone.utc).isoformat(),
                "connected": False, "mutated_database": False, "phase_certified": False,
                "remote_db_compatibility_verified": False}
    if options.self_test:
        assert parse_mysql_response("8.4.7\tcrecisol_dev\nSsl_cipher\tTLS_AES_256_GCM_SHA384\n")
        for fake in ("8.0.40\tcrecisol_dev\nSsl_cipher\tTLS_AES_256_GCM_SHA384",
                     "8.4.7\tother\nSsl_cipher\tTLS_AES_256_GCM_SHA384",
                     "8.4.7\tcrecisol_dev\nSsl_cipher\t"):
            try:
                parse_mysql_response(fake)
            except ValueError:
                continue
            raise AssertionError("insecure metadata unexpectedly accepted")
        response["status"] = "SELF_TEST_ONLY"
    elif options.plan:
        response.update(status="PREPARED_NOT_CONNECTED", authentication="operator private login-path",
                        tls_mode="VERIFY_IDENTITY", mysql_version_required="8.4.x",
                        query_scope="VERSION(), DATABASE(), Ssl_cipher status only")
    else:
        if not options.confirm_dev_only:
            response.update(status="BLOCKED_EXPLICIT_DEV_CONFIRMATION_REQUIRED")
        else:
            try:
                metadata = connect_readonly(options.ca)
                response.update(metadata)
                response.update(status="READ_ONLY_DEV_METADATA_VERIFIED", connected=True,
                                remote_db_compatibility_verified=True)
            except ValueError:
                response["status"] = "BLOCKED_CONNECTION_TLS_OR_VERSION"
    print(json.dumps(response, indent=2, sort_keys=True))
    return 0 if response["status"] in ("SELF_TEST_ONLY", "PREPARED_NOT_CONNECTED", "READ_ONLY_DEV_METADATA_VERIFIED") else 2


if __name__ == "__main__":
    sys.exit(main())
