-- Isolated crecisol_c05 schema only; assign this separate role offline.
-- No automatic user, runtime grant, direct table privileges or scheduling.
CREATE ROLE IF NOT EXISTS 'c06_privacy_local_worker';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c06_dispose_closed_sensitive_payloads TO 'c06_privacy_local_worker';
