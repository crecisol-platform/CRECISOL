#!/usr/bin/env bash
# C03-07: transient MySQL 8.4 UNIX-socket-only contract runner.
# It NEVER uses production hosts, DNS, ports, credentials or tenant data.
set -euo pipefail

CONTAINER="crecisol-c03-07-$RANDOM"
DB="crecisol_c03_07_test"
SOCKET_DIR="$(mktemp -d)"
chmod 777 "$SOCKET_DIR"

cleanup() {
  docker exec --user root "$CONTAINER" chown -R "$(id -u):$(id -g)" \
    /var/run/mysqld >/dev/null 2>&1 || true
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  rm -rf -- "$SOCKET_DIR"
}
trap cleanup EXIT
sql() {
  docker exec "$CONTAINER" mysql -u root --batch --skip-column-names -D "$DB" -e "$1"
}

docker run --rm -d --network none --name "$CONTAINER" \
  -v "$SOCKET_DIR:/var/run/mysqld" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" \
  mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902 >/dev/null

ready=0
for i in $(seq 1 90); do
  if sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo "FAIL C03-07: MySQL startup" >&2; exit 1; }
case "$(sql 'SELECT VERSION()')" in 8.4.*) : ;; *)
  echo "FAIL C03-07: MySQL 8.4 expected" >&2; exit 1 ;;
esac
[ -S "$SOCKET_DIR/mysqld.sock" ] || { echo "FAIL C03-07: local socket missing" >&2; exit 1; }

docker exec -i "$CONTAINER" mysql -u root -D "$DB" < database/c03_07_outbox_schema.sql
# Synthetic aggregate: exists ONLY in this disposable test database.
sql 'CREATE TABLE c03_07_test_aggregate(event_id BINARY(16) PRIMARY KEY) ENGINE=InnoDB'

RELAY_PASSWORD="$(openssl rand -hex 18)"
docker exec -i "$CONTAINER" mysql -u root -D "$DB" >/dev/null <<SQL
CREATE USER 'c03_07_runner'@'localhost' IDENTIFIED BY '$RELAY_PASSWORD';
GRANT SELECT, INSERT, UPDATE ON \`$DB\`.\`c03_07_outbox_events\` TO 'c03_07_runner'@'localhost';
GRANT SELECT, INSERT, UPDATE ON \`$DB\`.\`c03_07_inbox_receipts\` TO 'c03_07_runner'@'localhost';
GRANT SELECT, INSERT ON \`$DB\`.\`c03_07_test_aggregate\` TO 'c03_07_runner'@'localhost';
SQL
export CRECISOL_C03_07_TEST_DSN="Server=$SOCKET_DIR/mysqld.sock;Connection Protocol=Unix;User ID=c03_07_runner;Password=$RELAY_PASSWORD;Database=$DB;Pooling=False;"

dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj \
  -- mysql

# SQL permission denial must be enforced by MySQL itself, not source inspection.
if docker exec -e MYSQL_PWD="$RELAY_PASSWORD" "$CONTAINER" mysql -u c03_07_runner -D "$DB" \
  -e 'DROP TABLE c03_07_inbox_receipts' >/dev/null 2>&1; then
  echo 'FAIL C03-07: worker was granted DDL' >&2; exit 1
fi
if docker exec -e MYSQL_PWD="$RELAY_PASSWORD" "$CONTAINER" mysql -u c03_07_runner -D "$DB" \
  -e 'DELETE FROM c03_07_outbox_events' >/dev/null 2>&1; then
  echo 'FAIL C03-07: worker was granted DELETE' >&2; exit 1
fi
echo 'PASS C03-07 MySQL8.4: atomic aggregate/outbox rollback, fenced leases, retries, dead-letter, composite tenant FK, duplicate-safe receipts, no DDL/DELETE.'
echo 'LIMIT: disposable synthetic local foundation only; no private auth, external effects or production dispatch.'
