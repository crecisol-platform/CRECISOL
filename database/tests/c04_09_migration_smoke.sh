#!/usr/bin/env bash
# CRECISOL C04-09: isolated MySQL 8.4 compatibility/recovery smoke fixture.
# Entire test runs in ephemeral Docker --network none; no host port, tenant
# credentials, production migrations or active services.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c04-09-$RANDOM-$$"
PASS=0
DENIED=0
TMP="$(mktemp -d)"
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT
sql() { docker exec "$CONTAINER" mysql -uroot -N -B -e "$1"; }
ok() {
  local label="$1" expected="$2" actual="$3"
  if [ "$actual" != "$expected" ]; then echo "FAIL $label want=$expected got=$actual" >&2; exit 1; fi
  PASS=$((PASS+1)); echo "PASS C04-09 $label"
}
denied() {
  local label="$1" statement="$2" output
  if output="$(sql "$statement" 2>&1)"; then
    echo "FAIL C04-09 $label unexpectedly allowed: $output" >&2; exit 1
  fi
  DENIED=$((DENIED+1)); echo "PASS C04-09 denied $label"
}
docker run --rm --network none -d --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes "$IMAGE" >/dev/null
boot=0
ready=0
for _ in $(seq 1 120); do
  if [ "$boot" = 0 ] &&
     docker logs "$CONTAINER" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    boot=1
  fi
  if [ "$boot" = 1 ] && sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
if [ "$ready" = 0 ]; then echo "FAIL C04-09 MySQL boot"; exit 1; fi
case "$(sql 'SELECT VERSION()')" in 8.4.*) ;; *)
  echo 'FAIL MySQL 8.4 required' >&2; exit 1 ;;
esac

# Three independent throwaway schemas. DEDICATED A/B never share tables.
sql "CREATE DATABASE c04_09_shared;
CREATE DATABASE c04_09_ded_a;
CREATE DATABASE c04_09_ded_b;
CREATE TABLE c04_09_shared.tenants (
  tenant_id CHAR(1) NOT NULL PRIMARY KEY,
  active TINYINT NOT NULL CHECK(active IN(0,1))
) ENGINE=InnoDB;
CREATE TABLE c04_09_shared.resources (
  tenant_id CHAR(1) NOT NULL,
  resource_id INT NOT NULL,
  payload VARCHAR(30) NOT NULL,
  PRIMARY KEY(tenant_id,resource_id),
  CONSTRAINT fk_c0409_tenant FOREIGN KEY(tenant_id)
    REFERENCES c04_09_shared.tenants(tenant_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;
CREATE TABLE c04_09_ded_a.resources (
  resource_id INT NOT NULL PRIMARY KEY, payload VARCHAR(30) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE c04_09_ded_b.resources LIKE c04_09_ded_a.resources;
INSERT INTO c04_09_shared.tenants VALUES ('A',1),('B',1);
INSERT INTO c04_09_shared.resources VALUES('A',10,'ALPHA'),('B',10,'BETA');
INSERT INTO c04_09_ded_a.resources VALUES(10,'DED-ALPHA');
INSERT INTO c04_09_ded_b.resources VALUES(10,'DED-BETA');"

# Expand-only DDL, performed on synthetic SHARED and separately on DED A.
sql "ALTER TABLE c04_09_shared.resources ADD COLUMN migration_tag VARCHAR(20) NULL;
ALTER TABLE c04_09_ded_a.resources ADD COLUMN migration_tag VARCHAR(20) NULL;"
ok "shared expanded" "1" "$(sql "SELECT COUNT(*) FROM information_schema.columns
 WHERE table_schema='c04_09_shared' AND table_name='resources' AND column_name='migration_tag'")"
ok "target dedicated expanded" "1" "$(sql "SELECT COUNT(*) FROM information_schema.columns
 WHERE table_schema='c04_09_ded_a' AND table_name='resources' AND column_name='migration_tag'")"
ok "untargeted dedicated unchanged" "0" "$(sql "SELECT COUNT(*) FROM information_schema.columns
 WHERE table_schema='c04_09_ded_b' AND table_name='resources' AND column_name='migration_tag'")"
ok "tenant B untouched" "BETA" "$(sql "SELECT payload FROM c04_09_shared.resources WHERE tenant_id='B'")"
ok "dedicated B untouched" "DED-BETA" "$(sql "SELECT payload FROM c04_09_ded_b.resources WHERE resource_id=10")"
ok "shared composite PK preserved" "2" "$(sql "SELECT COUNT(*) FROM information_schema.statistics
 WHERE table_schema='c04_09_shared' AND table_name='resources' AND index_name='PRIMARY'")"
ok "shared tenant FK preserved" "1" "$(sql "SELECT COUNT(*) FROM information_schema.referential_constraints
 WHERE constraint_schema='c04_09_shared' AND constraint_name='fk_c0409_tenant'")"
denied "cross-tenant orphan FK" "INSERT INTO c04_09_shared.resources VALUES('C',20,'SPOOF',NULL)"
denied "invalid shared actor tenant" "INSERT INTO c04_09_shared.tenants VALUES('X',9)"

# Version-check before applying a destructive/duplicate migration: same
# version + different checksum must be rejected by a *protected ledger*.
sql "CREATE TABLE c04_09_ded_a.migration_ledger (
 version VARCHAR(24) NOT NULL PRIMARY KEY,
 checksum CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 status ENUM('prepared','applying','verified','failed') NOT NULL
) ENGINE=InnoDB;
INSERT INTO c04_09_ded_a.migration_ledger VALUES
 ('schema_2026_09',REPEAT('a',64),'verified');"
ok "ledger checksum immutable expectation" "$(printf 'a%.0s' {1..64})" "$(sql "
 SELECT checksum FROM c04_09_ded_a.migration_ledger WHERE version='schema_2026_09'")"
denied "duplicate schema version" "INSERT INTO c04_09_ded_a.migration_ledger
 VALUES('schema_2026_09',REPEAT('b',64),'verified')"

# MySQL DDL does implicit commit: ensure we do not report a transactional
# rollback. Deliberately create partial DDL then recover by explicit
# compensating DDL on isolated synthetic data only.
sql "ALTER TABLE c04_09_ded_a.resources ADD COLUMN partial_marker INT NULL;"
ok "partial DDL persists until compensation" "1" "$(sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema='c04_09_ded_a'
 AND table_name='resources' AND column_name='partial_marker'")"
sql "ALTER TABLE c04_09_ded_a.resources DROP COLUMN partial_marker;"
ok "compensating DDL restores schema" "0" "$(sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema='c04_09_ded_a'
 AND table_name='resources' AND column_name='partial_marker'")"
ok "data survives compensation" "DED-ALPHA" "$(sql "
 SELECT payload FROM c04_09_ded_a.resources WHERE resource_id=10")"
ok "other dedicated schema still unchanged" "DED-BETA" "$(sql "
 SELECT payload FROM c04_09_ded_b.resources WHERE resource_id=10")"

# Explicit compensating rollback for the reversible additive columns.
sql "ALTER TABLE c04_09_shared.resources DROP COLUMN migration_tag;
ALTER TABLE c04_09_ded_a.resources DROP COLUMN migration_tag;"
ok "shared rollback restored schema" "0" "$(sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema='c04_09_shared'
 AND table_name='resources' AND column_name='migration_tag'")"
ok "dedicated rollback restored schema" "0" "$(sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema='c04_09_ded_a'
 AND table_name='resources' AND column_name='migration_tag'")"
ok "shared A/B rows retained" "2" "$(sql "SELECT COUNT(*) FROM c04_09_shared.resources")"
echo "C04-09 MySQL8.4 disposable compatibility and compensating rollback: $PASS positive, $DENIED negative."
echo "LIMIT: no protected migration executor, physical restoration, fencing, auth or production DDL certified."
