-- C05-018 additive server-side inactivity policy. Never reapply 017.
-- Existing persistent volume needs verified SQL+key backup and separate manual gate.
-- Operator admin privilege is FALSE by default, not granted by migration.
ALTER TABLE c05_platform_operator
 ADD COLUMN can_manage_session_policy BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE c05_session_idle_policy (
 policy_id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
 idle_minutes SMALLINT UNSIGNED NOT NULL DEFAULT 30,
 revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
 changed_at_utc DATETIME(6) NOT NULL,
 changed_by BINARY(16) NULL,
 CONSTRAINT ck_c05_idle_policy_id CHECK(policy_id=1),
 CONSTRAINT ck_c05_idle_policy_minutes CHECK(idle_minutes BETWEEN 5 AND 120),
 CONSTRAINT ck_c05_idle_policy_revision CHECK(revision>0),
 CONSTRAINT fk_c05_idle_policy_admin FOREIGN KEY(changed_by) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
INSERT INTO c05_session_idle_policy(policy_id,idle_minutes,revision,changed_at_utc,changed_by)
 VALUES(1,30,1,UTC_TIMESTAMP(6),NULL);
