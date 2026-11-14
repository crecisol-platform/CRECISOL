-- CRECISOL C05-10: disposable MySQL8.4 admission and redacted access trace.
-- CONTRACT/TEST ONLY, never a production migration, telemetry pipeline or login.
-- No raw IP, mailbox, password, cookie, token, PHC, session, URL or payload.
-- Full runner privileges: EXECUTE on two named routines, never table DML/SELECT.
CREATE TABLE c05_10_policies (
 tenant_id BINARY(16) NOT NULL,
 purpose VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 subject_limit SMALLINT UNSIGNED NOT NULL,
 source_limit SMALLINT UNSIGNED NOT NULL,
 window_seconds SMALLINT UNSIGNED NOT NULL,
 cooldown_seconds SMALLINT UNSIGNED NOT NULL,
 enabled TINYINT UNSIGNED NOT NULL,
 host_revision BIGINT UNSIGNED NOT NULL,
 revision BIGINT UNSIGNED NOT NULL,
 changed_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (tenant_id,purpose),
 CONSTRAINT ck_c05_10_policy_action CHECK (purpose IN ('LOGIN','RECOVERY','MFA')),
 CONSTRAINT ck_c05_10_policy_limits CHECK (
    subject_limit BETWEEN 1 AND 12 AND source_limit BETWEEN subject_limit AND 100
    AND window_seconds BETWEEN 60 AND 3600
    AND cooldown_seconds BETWEEN 60 AND 3600
    AND enabled IN (0,1) AND host_revision>0 AND revision>0)
) ENGINE=InnoDB;

CREATE TABLE c05_10_buckets (
 tenant_id BINARY(16) NOT NULL,
 purpose VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 dimension VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 digest BINARY(32) NOT NULL,
 attempts BIGINT UNSIGNED NOT NULL,
 window_started_utc DATETIME(6) NOT NULL,
 blocked_until_utc DATETIME(6) NULL,
 updated_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (tenant_id,purpose,dimension,digest),
 CONSTRAINT fk_c05_10_bucket_policy FOREIGN KEY (tenant_id,purpose)
    REFERENCES c05_10_policies(tenant_id,purpose)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT ck_c05_10_bucket_dimension CHECK(dimension IN ('SOURCE','SUBJECT'))
) ENGINE=InnoDB;

CREATE TABLE c05_10_access_traces (
 attempt_id BINARY(16) NOT NULL,
 tenant_id BINARY(16) NOT NULL,
 purpose VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 source_digest BINARY(32) NOT NULL,
 subject_digest BINARY(32) NOT NULL,
 disposition VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 policy_revision BIGINT UNSIGNED NOT NULL,
 occurred_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (attempt_id),
 UNIQUE KEY uq_c05_10_trace_scope (tenant_id,purpose,attempt_id),
 KEY ix_c05_10_trace_tenant_time (tenant_id,purpose,occurred_utc),
 CONSTRAINT fk_c05_10_trace_policy FOREIGN KEY (tenant_id,purpose)
    REFERENCES c05_10_policies(tenant_id,purpose)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT ck_c05_10_disposition CHECK(disposition IN ('ADMITTED','LIMITED')),
 CONSTRAINT ck_c05_10_trace_revision CHECK(policy_revision>0)
) ENGINE=InnoDB;

CREATE TABLE c05_10_access_outcomes (
 attempt_id BINARY(16) NOT NULL,
 tenant_id BINARY(16) NOT NULL,
 purpose VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 outcome VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 completed_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (attempt_id),
 CONSTRAINT fk_c05_10_outcome_trace FOREIGN KEY
    (tenant_id,purpose,attempt_id)
    REFERENCES c05_10_access_traces(tenant_id,purpose,attempt_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT ck_c05_10_outcome CHECK(outcome IN ('VERIFIED','REJECTED'))
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER c05_10_trace_no_update
BEFORE UPDATE ON c05_10_access_traces FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-10 append-only trace'; END$$
CREATE TRIGGER c05_10_trace_no_delete
BEFORE DELETE ON c05_10_access_traces FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-10 append-only trace'; END$$
CREATE TRIGGER c05_10_outcome_no_update
BEFORE UPDATE ON c05_10_access_outcomes FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-10 append-only outcome'; END$$
CREATE TRIGGER c05_10_outcome_no_delete
BEFORE DELETE ON c05_10_access_outcomes FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-10 append-only outcome'; END$$

-- SOURCE-first locking is mandatory for all nodes/threads. Pre-verification
-- admission consumes the budget BEFORE expensive C05-04 Argon2id.
-- All source/subject counters + trace are one transaction with DB UTC time.
-- Active cooldown replies never insert a trace per repeated blocked packet.
CREATE PROCEDURE c05_10_admit_once(
 IN p_tenant BINARY(16), IN p_purpose VARCHAR(10),
 IN p_host_revision BIGINT UNSIGNED,
 IN p_attempt BINARY(16), IN p_source BINARY(32),
 IN p_subject BINARY(32)
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOLEAN DEFAULT FALSE;
 DECLARE v_sub_limit SMALLINT UNSIGNED;
 DECLARE v_src_limit SMALLINT UNSIGNED;
 DECLARE v_window SMALLINT UNSIGNED;
 DECLARE v_cooldown SMALLINT UNSIGNED;
 DECLARE v_policy_revision BIGINT UNSIGNED;
 DECLARE v_enabled TINYINT UNSIGNED;
 DECLARE v_host_revision BIGINT UNSIGNED;
 DECLARE v_count BIGINT UNSIGNED;
 DECLARE v_started DATETIME(6);
 DECLARE v_locked DATETIME(6);
 DECLARE v_now DATETIME(6);
 DECLARE v_decision VARCHAR(8) DEFAULT 'DENIED';
 DECLARE v_write_log BOOLEAN DEFAULT TRUE;
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

 IF p_tenant IS NULL OR p_attempt IS NULL OR p_source IS NULL OR
    p_subject IS NULL OR p_host_revision IS NULL OR
    p_purpose NOT IN ('LOGIN','RECOVERY','MFA') OR
    p_purpose IS NULL THEN
   SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT subject_limit,source_limit,window_seconds,cooldown_seconds,
        revision,enabled,host_revision
 INTO v_sub_limit,v_src_limit,v_window,v_cooldown,
      v_policy_revision,v_enabled,v_host_revision
 FROM c05_10_policies
 WHERE tenant_id=p_tenant AND purpose=p_purpose FOR UPDATE;
 IF v_missing OR v_enabled<>1 OR v_policy_revision<1 OR
    v_host_revision<>p_host_revision THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;

 -- Source bucket locks first; even unlimited distinct usernames from one
 -- origin cannot create fresh SUBJECT rows after source cooldown triggers.
 INSERT IGNORE INTO c05_10_buckets
 (tenant_id,purpose,dimension,digest,attempts,window_started_utc,
  blocked_until_utc,updated_utc)
 VALUES (p_tenant,p_purpose,'SOURCE',p_source,0,v_now,NULL,v_now);
 SET v_missing=FALSE;
 SELECT attempts,window_started_utc,blocked_until_utc
 INTO v_count,v_started,v_locked FROM c05_10_buckets
 WHERE tenant_id=p_tenant AND purpose=p_purpose AND
       dimension='SOURCE' AND digest=p_source FOR UPDATE;
 IF v_missing OR v_started>v_now THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 IF v_locked IS NOT NULL AND v_locked>v_now THEN
    SET v_decision='LIMITED'; SET v_write_log=FALSE;
 ELSE
    IF v_started<=DATE_SUB(v_now,INTERVAL v_window SECOND) OR
       (v_locked IS NOT NULL AND v_locked<=v_now) THEN
      SET v_count=0; SET v_started=v_now; SET v_locked=NULL;
    END IF;
    IF v_count>=v_src_limit THEN
      SET v_decision='LIMITED';
      SET v_locked=DATE_ADD(v_now,INTERVAL v_cooldown SECOND);
    ELSE
      SET v_decision='ADMITTED';
      SET v_count=v_count+1;
    END IF;
    UPDATE c05_10_buckets
       SET attempts=v_count,window_started_utc=v_started,
           blocked_until_utc=v_locked,updated_utc=v_now
     WHERE tenant_id=p_tenant AND purpose=p_purpose
       AND dimension='SOURCE' AND digest=p_source;
 END IF;

 IF v_decision='ADMITTED' THEN
    INSERT IGNORE INTO c05_10_buckets
    (tenant_id,purpose,dimension,digest,attempts,window_started_utc,
     blocked_until_utc,updated_utc)
    VALUES (p_tenant,p_purpose,'SUBJECT',p_subject,0,v_now,NULL,v_now);
    SET v_missing=FALSE;
    SELECT attempts,window_started_utc,blocked_until_utc
    INTO v_count,v_started,v_locked FROM c05_10_buckets
    WHERE tenant_id=p_tenant AND purpose=p_purpose
      AND dimension='SUBJECT' AND digest=p_subject FOR UPDATE;
    IF v_missing OR v_started>v_now THEN
      ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
    END IF;
    IF v_locked IS NOT NULL AND v_locked>v_now THEN
       SET v_decision='LIMITED'; SET v_write_log=FALSE;
    ELSE
       IF v_started<=DATE_SUB(v_now,INTERVAL v_window SECOND) OR
         (v_locked IS NOT NULL AND v_locked<=v_now) THEN
         SET v_count=0; SET v_started=v_now; SET v_locked=NULL;
       END IF;
       IF v_count>=v_sub_limit THEN
         SET v_decision='LIMITED';
         SET v_locked=DATE_ADD(v_now,INTERVAL v_cooldown SECOND);
       ELSE
         SET v_count=v_count+1;
       END IF;
       UPDATE c05_10_buckets
          SET attempts=v_count,window_started_utc=v_started,
              blocked_until_utc=v_locked,updated_utc=v_now
        WHERE tenant_id=p_tenant AND purpose=p_purpose
          AND dimension='SUBJECT' AND digest=p_subject;
    END IF;
 END IF;

 IF v_write_log THEN
    INSERT INTO c05_10_access_traces
      (attempt_id,tenant_id,purpose,source_digest,subject_digest,
       disposition,policy_revision,occurred_utc)
    VALUES (p_attempt,p_tenant,p_purpose,p_source,p_subject,
            v_decision,v_policy_revision,v_now);
 END IF;
 COMMIT;
 SELECT v_decision AS result;
END$$

-- Auth result is never supplied by a browser; this is a backend-only
-- completion after C05-04 Argon2id. One attempt can finish at most once.
-- A denied or forged attempt cannot write a fabricated VERIFIED outcome.
CREATE PROCEDURE c05_10_complete_once(
 IN p_tenant BINARY(16), IN p_purpose VARCHAR(10),
 IN p_attempt BINARY(16), IN p_source BINARY(32),
 IN p_subject BINARY(32), IN p_outcome VARCHAR(8)
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOLEAN DEFAULT FALSE;
 DECLARE v_tenant BINARY(16);
 DECLARE v_purpose VARCHAR(10);
 DECLARE v_source BINARY(32);
 DECLARE v_subject BINARY(32);
 DECLARE v_disposition VARCHAR(8);
 DECLARE v_existing BINARY(16);
 DECLARE v_now DATETIME(6);
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 IF p_tenant IS NULL OR p_purpose NOT IN ('LOGIN','RECOVERY','MFA') OR
    p_purpose IS NULL OR p_attempt IS NULL OR p_source IS NULL OR
    p_subject IS NULL OR p_outcome NOT IN ('VERIFIED','REJECTED') OR
    p_outcome IS NULL THEN
   SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT tenant_id,purpose,source_digest,subject_digest,disposition
 INTO v_tenant,v_purpose,v_source,v_subject,v_disposition
 FROM c05_10_access_traces WHERE attempt_id=p_attempt FOR UPDATE;
 IF v_missing OR v_tenant<>p_tenant OR v_purpose<>p_purpose OR
    v_source<>p_source OR v_subject<>p_subject OR
    v_disposition<>'ADMITTED' THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SET v_missing=FALSE;
 SELECT attempt_id INTO v_existing FROM c05_10_access_outcomes
 WHERE attempt_id=p_attempt FOR UPDATE;
 IF NOT v_missing THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 INSERT INTO c05_10_access_outcomes
   (attempt_id,tenant_id,purpose,outcome,completed_utc)
 VALUES (p_attempt,p_tenant,p_purpose,p_outcome,v_now);
 COMMIT;
 SELECT 'RECORDED' AS result;
END$$
DELIMITER ;
