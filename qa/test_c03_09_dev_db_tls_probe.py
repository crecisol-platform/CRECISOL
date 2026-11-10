#!/usr/bin/env python3
"""No-network negative tests of C03-09 optional external DEV read-only probe."""
import contextlib
import io
import json
import os
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
from unittest import TestCase, main, mock

sys.path.insert(0, str(Path(__file__).parent))
import c03_09_dev_db_tls_probe as subject


class DevDbProbeSafetyTests(TestCase):
    def test_plan_is_not_a_network_call(self):
        with mock.patch.object(subject.subprocess, "run", side_effect=AssertionError("must not connect")), \
             contextlib.redirect_stdout(io.StringIO()) as out:
            self.assertEqual(subject.main(["--plan"]), 0)
        report = json.loads(out.getvalue())
        self.assertFalse(report["connected"])
        self.assertFalse(report["phase_certified"])
        self.assertFalse(report["remote_db_compatibility_verified"])
        self.assertEqual(report["tls_mode"], "VERIFY_IDENTITY")

    def test_missing_confirmation_denies_connection(self):
        with mock.patch.object(subject, "connect_readonly", side_effect=AssertionError("must not connect")), \
             contextlib.redirect_stdout(io.StringIO()) as out:
            self.assertEqual(subject.main(["--connect-readonly"]), 2)
        self.assertIn("BLOCKED_EXPLICIT", out.getvalue())

    def test_sql_contains_only_select_and_show(self):
        sql = subject.FIXED_SQL
        self.assertEqual(sql, "SELECT VERSION(), DATABASE(); SHOW SESSION STATUS LIKE 'Ssl_cipher';")
        self.assertFalse(any(word in sql.upper() for word in ("DELETE", "INSERT", "ALTER", "DROP", "UPDATE", "CREATE")))

    def test_argv_forces_tcp_verified_tls_and_no_password(self):
        args = subject.mysql_command("mysql", Path("/local/ca.pem"))
        self.assertIn("--no-defaults", args)
        self.assertIn("--ssl-mode=VERIFY_IDENTITY", args)
        self.assertIn("--protocol=TCP", args)
        self.assertIn("--database=crecisol_dev", args)
        self.assertIn("--local-infile=0", args)
        self.assertFalse(any("password" in x.lower() for x in args))
        self.assertFalse(any("179." in x for x in args))

    def test_environment_drops_mysql_pwd_and_remote_settings(self):
        with mock.patch.dict(os.environ, {"MYSQL_PWD": "PRIVATE", "MYSQL_TEST_LOGIN_FILE": "secrets",
                                             "CRECISOL_C01_READONLY_DSN": "PRIVATE"}):
            env = subject.safe_env()
        for key in ("MYSQL_PWD", "MYSQL_TEST_LOGIN_FILE", "CRECISOL_C01_READONLY_DSN"):
            self.assertNotIn(key, env)

    def test_bad_version_is_denied(self):
        with self.assertRaisesRegex(ValueError, "not verified MySQL 8.4"):
            subject.parse_mysql_response("8.0.42\tcrecisol_dev\nSsl_cipher\tTLS_AES_256_GCM_SHA384\n")

    def test_wrong_database_is_denied(self):
        with self.assertRaisesRegex(ValueError, "does not match"):
            subject.parse_mysql_response("8.4.7\tproduction\nSsl_cipher\tTLS_AES_256_GCM_SHA384\n")

    def test_missing_cipher_is_denied(self):
        for result in ("8.4.7\tcrecisol_dev\nSsl_cipher\t\n", "8.4.7\tcrecisol_dev\nSsl_cipher\tNONE\n"):
            with self.subTest(result=result), self.assertRaises(ValueError):
                subject.parse_mysql_response(result)

    def test_extra_rows_are_denied(self):
        with self.assertRaises(ValueError):
            subject.parse_mysql_response("8.4.7\tcrecisol_dev\nSsl_cipher\tTLS_AES_256_GCM_SHA384\nsecret\n")

    def test_certificate_must_exist_be_pem_and_be_absolute(self):
        with TemporaryDirectory() as temp:
            p = Path(temp) / "ca.pem"
            with self.assertRaises(ValueError):
                subject.validate_ca("ca.pem")
            with self.assertRaises(ValueError):
                subject.validate_ca(str(p))
            p.write_bytes(b"X" * 256)
            with self.assertRaises(ValueError):
                subject.validate_ca(str(p))
            p.write_bytes(b"-----BEGIN CERTIFICATE-----\n" + b"Q" * 100 + b"\n-----END CERTIFICATE-----\n")
            self.assertEqual(subject.validate_ca(str(p)), p)
            link = Path(temp) / "symlink.pem"
            link.symlink_to(p)
            with self.assertRaises(ValueError):
                subject.validate_ca(str(link))

    def test_cli_nonzero_keeps_diagnostics_private(self):
        with TemporaryDirectory() as temp:
            ca = Path(temp) / "ca.pem"
            ca.write_bytes(b"-----BEGIN CERTIFICATE-----\n" + b"Q" * 100)
            fake = mock.Mock(returncode=1, stdout="", stderr="SECRET account / PRIVATE HOST")
            with mock.patch.object(subject.shutil, "which", return_value="/usr/bin/mysql"), \
                 mock.patch.object(subject.subprocess, "run", return_value=fake):
                with self.assertRaises(ValueError) as err:
                    subject.connect_readonly(str(ca))
            self.assertNotIn("SECRET", str(err.exception))
            self.assertNotIn("PRIVATE", str(err.exception))

    def test_success_metadata_but_never_certification(self):
        metadata = {"mysql_server_version": "8.4.7", "database_matches": True, "tls_cipher_present": True}
        with mock.patch.object(subject, "connect_readonly", return_value=metadata), \
             contextlib.redirect_stdout(io.StringIO()) as out:
            self.assertEqual(subject.main(["--connect-readonly", "--confirm-dev-only", "--ca", "/local/ca.pem"]), 0)
        report = json.loads(out.getvalue())
        self.assertEqual(report["status"], "READ_ONLY_DEV_METADATA_VERIFIED")
        self.assertTrue(report["remote_db_compatibility_verified"])
        self.assertFalse(report["phase_certified"])

    def test_parse_rejects_malformed_cipher(self):
        with self.assertRaises(ValueError):
            subject.parse_mysql_response("8.4.7\tcrecisol_dev\nSsl_cipher\tTLS  AES\n")


if __name__ == "__main__":
    main()
