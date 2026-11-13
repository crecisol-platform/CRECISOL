-- CRECISOL C04-10 synthetic tenant artifact fixture ONLY.
-- Fresh --network none MySQL 8.4 container; never use as product migration.
-- No real cache, queue, media, object store, exports or HTTP authentication.
CREATE DATABASE c04_10_disposable;
USE c04_10_disposable;

CREATE TABLE tenants (
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 state ENUM('active','suspended','deactivated') NOT NULL,
 access_epoch BIGINT UNSIGNED NOT NULL,
 CONSTRAINT ck_c10_epoch CHECK(access_epoch > 0)
) ENGINE=InnoDB;
CREATE TABLE branches (
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 state ENUM('active','suspended') NOT NULL,
 revision BIGINT UNSIGNED NOT NULL,
 PRIMARY KEY(tenant_id,branch_id),
 CONSTRAINT fk_c10_branch_tenant FOREIGN KEY(tenant_id)
  REFERENCES tenants(tenant_id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB;
CREATE TABLE resources (
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 resource_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 owner_actor CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 state ENUM('active','suspended') NOT NULL,
 revision BIGINT UNSIGNED NOT NULL,
 PRIMARY KEY(tenant_id,branch_id,resource_id),
 CONSTRAINT fk_c10_resource_branch FOREIGN KEY(tenant_id,branch_id)
  REFERENCES branches(tenant_id,branch_id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB;

-- Synthetic DB account binding is a test of SQL least privilege only, NOT
-- actual tenant user authentication or a substitute for server JWT/MFA.
CREATE TABLE test_account_binding (
 sql_login VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 enabled TINYINT NOT NULL DEFAULT 1,
 CONSTRAINT fk_c10_account_tenant FOREIGN KEY(tenant_id)
  REFERENCES tenants(tenant_id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT ck_c10_account_enabled CHECK(enabled IN (0,1))
) ENGINE=InnoDB;

CREATE TABLE decisions (
 operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 kind ENUM('JOB','MEDIA','EXPORT') NOT NULL,
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 resource_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tenant_epoch BIGINT UNSIGNED NOT NULL,
 branch_revision BIGINT UNSIGNED NOT NULL,
 resource_revision BIGINT UNSIGNED NOT NULL,
 approved TINYINT NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 UNIQUE KEY uq_c10_decision_tuple
  (operation_id,tenant_id,branch_id,resource_id,actor_id,kind),
 CONSTRAINT fk_c10_decision_resource FOREIGN KEY(tenant_id,branch_id,resource_id)
  REFERENCES resources(tenant_id,branch_id,resource_id)
  ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT ck_c10_decision_approved CHECK(approved IN (0,1))
) ENGINE=InnoDB;

CREATE TABLE cache_entries (
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 resource_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tenant_epoch BIGINT UNSIGNED NOT NULL,
 branch_revision BIGINT UNSIGNED NOT NULL,
 resource_revision BIGINT UNSIGNED NOT NULL,
 cache_key CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 marker VARCHAR(32) NOT NULL,
 PRIMARY KEY(tenant_id,branch_id,resource_id,actor_id,cache_key),
 UNIQUE KEY uq_c10_cache_key_global(cache_key),
 CONSTRAINT fk_c10_cache_resource FOREIGN KEY(tenant_id,branch_id,resource_id)
  REFERENCES resources(tenant_id,branch_id,resource_id)
  ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT ck_c10_cache_key CHECK(REGEXP_LIKE(cache_key,'^[0-9a-f]{64}$','c'))
) ENGINE=InnoDB;

CREATE TABLE job_records (
 operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 kind ENUM('JOB','MEDIA','EXPORT') NOT NULL DEFAULT 'JOB',
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 resource_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tenant_epoch BIGINT UNSIGNED NOT NULL,
 branch_revision BIGINT UNSIGNED NOT NULL,
 resource_revision BIGINT UNSIGNED NOT NULL,
 status ENUM('pending','running','done','blocked') NOT NULL DEFAULT 'pending',
 fence BIGINT UNSIGNED NOT NULL DEFAULT 0,
 CONSTRAINT ck_c10_job_kind CHECK(kind='JOB'),
 CONSTRAINT fk_c10_job_decision FOREIGN KEY
  (operation_id,tenant_id,branch_id,resource_id,actor_id,kind)
  REFERENCES decisions(operation_id,tenant_id,branch_id,resource_id,actor_id,kind)
  ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB;

-- No user bytes, blob, URL, DSN, presigned download, PII or object payload:
-- these are protected metadata handles only. Cloudflare R2 is NOT connected.
CREATE TABLE media_refs (
 operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 kind ENUM('JOB','MEDIA','EXPORT') NOT NULL DEFAULT 'MEDIA',
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 resource_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tenant_epoch BIGINT UNSIGNED NOT NULL,
 branch_revision BIGINT UNSIGNED NOT NULL,
 resource_revision BIGINT UNSIGNED NOT NULL,
 object_reference CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 version BIGINT UNSIGNED NOT NULL,
 UNIQUE KEY uq_c10_media_ref(object_reference),
 CONSTRAINT ck_c10_media_kind CHECK(kind='MEDIA'),
 CONSTRAINT ck_c10_media_version CHECK(version > 0),
 CONSTRAINT ck_c10_media_key CHECK(REGEXP_LIKE(object_reference,'^[0-9a-f]{64}$','c')),
 CONSTRAINT fk_c10_media_decision FOREIGN KEY
  (operation_id,tenant_id,branch_id,resource_id,actor_id,kind)
  REFERENCES decisions(operation_id,tenant_id,branch_id,resource_id,actor_id,kind)
  ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE export_manifests (
 operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 kind ENUM('JOB','MEDIA','EXPORT') NOT NULL DEFAULT 'EXPORT',
 tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 branch_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 resource_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tenant_epoch BIGINT UNSIGNED NOT NULL,
 branch_revision BIGINT UNSIGNED NOT NULL,
 resource_revision BIGINT UNSIGNED NOT NULL,
 manifest_reference CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 version BIGINT UNSIGNED NOT NULL,
 UNIQUE KEY uq_c10_export_ref(manifest_reference),
 CONSTRAINT ck_c10_export_kind CHECK(kind='EXPORT'),
 CONSTRAINT ck_c10_export_version CHECK(version > 0),
 CONSTRAINT ck_c10_export_key CHECK(REGEXP_LIKE(manifest_reference,'^[0-9a-f]{64}$','c')),
 CONSTRAINT fk_c10_export_decision FOREIGN KEY
  (operation_id,tenant_id,branch_id,resource_id,actor_id,kind)
  REFERENCES decisions(operation_id,tenant_id,branch_id,resource_id,actor_id,kind)
  ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB;

DELIMITER //
-- Caller identity is the authenticated synthetic SQL login, never
-- p_tenant supplied by a client. Access only via these test procedures.
CREATE PROCEDURE claim_job(IN p_operation CHAR(32))
SQL SECURITY DEFINER
BEGIN
 DECLARE v_login VARCHAR(32) DEFAULT NULL;
 DECLARE v_tenant CHAR(32) DEFAULT NULL;
 DECLARE v_actor CHAR(32) DEFAULT NULL;
 DECLARE v_enabled TINYINT DEFAULT NULL;
 DECLARE v_tstate VARCHAR(16) DEFAULT NULL;
 DECLARE v_epoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_dtenant CHAR(32) DEFAULT NULL;
 DECLARE v_dactor CHAR(32) DEFAULT NULL;
 DECLARE v_depoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_dbranchrev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_dresourcerev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_actualbranchrev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_actualresourcerev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_jobbranchrev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_jobresourcerev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_jobepoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_approved TINYINT DEFAULT NULL;
 DECLARE v_expires DATETIME(6) DEFAULT NULL;
 DECLARE v_branch_state VARCHAR(16) DEFAULT NULL;
 DECLARE v_resource_state VARCHAR(16) DEFAULT NULL;
 DECLARE v_owner CHAR(32) DEFAULT NULL;
 DECLARE v_status VARCHAR(16) DEFAULT NULL;
 DECLARE v_fence BIGINT UNSIGNED DEFAULT NULL;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 SET v_login=SUBSTRING_INDEX(USER(),'@',1);
 START TRANSACTION;
 SELECT tenant_id,actor_id,enabled INTO v_tenant,v_actor,v_enabled
 FROM test_account_binding WHERE sql_login=v_login FOR UPDATE;
 IF v_tenant IS NULL OR v_enabled <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No trusted tenant runner binding';
 END IF;
 SELECT state,access_epoch INTO v_tstate,v_epoch
 FROM tenants WHERE tenant_id=v_tenant FOR UPDATE;
 SELECT d.tenant_id,d.actor_id,d.tenant_epoch,d.branch_revision,
        d.resource_revision,d.approved,d.expires_at_utc,
        b.state,b.revision,r.state,r.revision,r.owner_actor,
        j.tenant_epoch,j.branch_revision,j.resource_revision,j.status,j.fence
 INTO v_dtenant,v_dactor,v_depoch,v_dbranchrev,v_dresourcerev,
      v_approved,v_expires,v_branch_state,v_actualbranchrev,
      v_resource_state,v_actualresourcerev,v_owner,
      v_jobepoch,v_jobbranchrev,v_jobresourcerev,v_status,v_fence
 FROM decisions d
 JOIN job_records j ON j.operation_id=d.operation_id
 JOIN branches b ON b.tenant_id=d.tenant_id AND b.branch_id=d.branch_id
 JOIN resources r ON r.tenant_id=d.tenant_id AND r.branch_id=d.branch_id
   AND r.resource_id=d.resource_id
 WHERE d.operation_id=p_operation AND d.kind='JOB'
 FOR UPDATE;
 IF v_dtenant IS NULL OR v_dtenant <> v_tenant OR v_dactor <> v_actor OR
    v_depoch <> v_epoch OR v_tstate <> 'active' OR
    v_approved <> 1 OR v_expires <= UTC_TIMESTAMP(6) OR
    v_branch_state <> 'active' OR v_resource_state <> 'active' OR
    v_dbranchrev <> v_actualbranchrev OR v_dresourcerev <> v_actualresourcerev OR
    v_jobbranchrev <> v_dbranchrev OR v_jobresourcerev <> v_dresourcerev OR
    v_jobepoch <> v_depoch OR v_owner <> v_actor OR
    v_fence >= 18446744073709551614 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Tenant job scope revoked or mismatched';
 END IF;
 IF v_status='pending' THEN
  UPDATE job_records SET status='running',fence=fence+1
  WHERE operation_id=p_operation AND status='pending' AND tenant_epoch=v_epoch;
  IF ROW_COUNT() <> 1 THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Job CAS/fence lost';
  END IF;
  COMMIT;
  SELECT 'CLAIMED:1' AS outcome;
 ELSE
  COMMIT;
  SELECT 'BUSY' AS outcome; -- not a grant or execution
 END IF;
END//

CREATE PROCEDURE ack_job(IN p_operation CHAR(32), IN p_fence BIGINT UNSIGNED)
SQL SECURITY DEFINER
BEGIN
 DECLARE v_login VARCHAR(32) DEFAULT NULL;
 DECLARE v_tenant CHAR(32) DEFAULT NULL;
 DECLARE v_actor CHAR(32) DEFAULT NULL;
 DECLARE v_enabled TINYINT DEFAULT NULL;
 DECLARE v_tstate VARCHAR(16) DEFAULT NULL;
 DECLARE v_epoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_dtenant CHAR(32) DEFAULT NULL;
 DECLARE v_dactor CHAR(32) DEFAULT NULL;
 DECLARE v_depoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_dbranchrev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_dresourcerev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_actualbranchrev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_actualresourcerev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_jobbranchrev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_jobresourcerev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_jobepoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_approved TINYINT DEFAULT NULL;
 DECLARE v_expires DATETIME(6) DEFAULT NULL;
 DECLARE v_branch_state VARCHAR(16) DEFAULT NULL;
 DECLARE v_resource_state VARCHAR(16) DEFAULT NULL;
 DECLARE v_owner CHAR(32) DEFAULT NULL;
 DECLARE v_status VARCHAR(16) DEFAULT NULL;
 DECLARE v_fence BIGINT UNSIGNED DEFAULT NULL;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 SET v_login=SUBSTRING_INDEX(USER(),'@',1);
 START TRANSACTION;
 SELECT tenant_id,actor_id,enabled INTO v_tenant,v_actor,v_enabled
 FROM test_account_binding WHERE sql_login=v_login FOR UPDATE;
 IF v_tenant IS NULL OR v_enabled <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No tenant runner binding for acknowledgement';
 END IF;
 SELECT state,access_epoch INTO v_tstate,v_epoch
 FROM tenants WHERE tenant_id=v_tenant FOR UPDATE;
 SELECT d.tenant_id,d.actor_id,d.tenant_epoch,d.branch_revision,
        d.resource_revision,d.approved,d.expires_at_utc,
        b.state,b.revision,r.state,r.revision,r.owner_actor,
        j.tenant_epoch,j.branch_revision,j.resource_revision,j.status,j.fence
 INTO v_dtenant,v_dactor,v_depoch,v_dbranchrev,v_dresourcerev,
      v_approved,v_expires,v_branch_state,v_actualbranchrev,
      v_resource_state,v_actualresourcerev,v_owner,
      v_jobepoch,v_jobbranchrev,v_jobresourcerev,v_status,v_fence
 FROM decisions d JOIN job_records j ON j.operation_id=d.operation_id
 JOIN branches b ON b.tenant_id=d.tenant_id AND b.branch_id=d.branch_id
 JOIN resources r ON r.tenant_id=d.tenant_id AND r.branch_id=d.branch_id
   AND r.resource_id=d.resource_id
 WHERE d.operation_id=p_operation AND d.kind='JOB' FOR UPDATE;
 IF v_dtenant IS NULL OR v_dtenant <> v_tenant OR v_dactor <> v_actor OR
    v_tstate <> 'active' OR v_depoch <> v_epoch OR
    v_approved <> 1 OR v_expires <= UTC_TIMESTAMP(6) OR
    v_branch_state <> 'active' OR v_resource_state <> 'active' OR
    v_dbranchrev <> v_actualbranchrev OR v_dresourcerev <> v_actualresourcerev OR
    v_jobbranchrev <> v_dbranchrev OR v_jobresourcerev <> v_dresourcerev OR
    v_jobepoch <> v_depoch OR v_owner <> v_actor OR
    v_status <> 'running' OR v_fence <> p_fence THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Stale or untrusted tenant job acknowledgement';
 END IF;
 UPDATE job_records SET status='done'
 WHERE operation_id=p_operation AND status='running' AND fence=p_fence;
 IF ROW_COUNT() <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Tenant job acknowledgement CAS lost';
 END IF;
 COMMIT;
 SELECT 'ACKED' AS outcome;
END//

CREATE PROCEDURE read_private_cache(IN p_key CHAR(64))
SQL SECURITY DEFINER
BEGIN
 DECLARE v_tenant CHAR(32) DEFAULT NULL;
 DECLARE v_actor CHAR(32) DEFAULT NULL;
 DECLARE v_enabled TINYINT DEFAULT NULL;
 DECLARE v_marker VARCHAR(32) DEFAULT NULL;
 SELECT tenant_id,actor_id,enabled INTO v_tenant,v_actor,v_enabled
 FROM test_account_binding WHERE sql_login=SUBSTRING_INDEX(USER(),'@',1);
 IF v_tenant IS NULL OR v_enabled <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No private cache reader identity';
 END IF;
 SELECT c.marker INTO v_marker FROM cache_entries c
 JOIN tenants t ON t.tenant_id=c.tenant_id
 JOIN branches b ON b.tenant_id=c.tenant_id AND b.branch_id=c.branch_id
 JOIN resources r ON r.tenant_id=c.tenant_id AND r.branch_id=c.branch_id
    AND r.resource_id=c.resource_id
 WHERE c.cache_key=p_key AND c.tenant_id=v_tenant AND c.actor_id=v_actor
   AND t.state='active' AND t.access_epoch=c.tenant_epoch
   AND b.state='active' AND r.state='active' AND r.owner_actor=v_actor
   AND b.revision=c.branch_revision AND r.revision=c.resource_revision;
 IF v_marker IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Private cache miss or denied';
 END IF;
 SELECT v_marker AS marker;
END//

CREATE PROCEDURE read_private_media(IN p_operation CHAR(32))
SQL SECURITY DEFINER
BEGIN
 DECLARE v_tenant CHAR(32) DEFAULT NULL;
 DECLARE v_actor CHAR(32) DEFAULT NULL;
 DECLARE v_enabled TINYINT DEFAULT NULL;
 DECLARE v_reference CHAR(64) DEFAULT NULL;
 SELECT tenant_id,actor_id,enabled INTO v_tenant,v_actor,v_enabled
 FROM test_account_binding WHERE sql_login=SUBSTRING_INDEX(USER(),'@',1);
 IF v_tenant IS NULL OR v_enabled <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No private media reader identity';
 END IF;
 SELECT m.object_reference INTO v_reference FROM media_refs m
 JOIN decisions d ON d.operation_id=m.operation_id
 JOIN tenants t ON t.tenant_id=m.tenant_id
 JOIN branches b ON b.tenant_id=m.tenant_id AND b.branch_id=m.branch_id
 JOIN resources r ON r.tenant_id=m.tenant_id AND r.branch_id=m.branch_id
    AND r.resource_id=m.resource_id
 WHERE m.operation_id=p_operation AND m.tenant_id=v_tenant AND m.actor_id=v_actor
   AND d.approved=1 AND d.expires_at_utc>UTC_TIMESTAMP(6)
   AND t.state='active' AND t.access_epoch=m.tenant_epoch
   AND b.state='active' AND r.state='active' AND r.owner_actor=v_actor
   AND b.revision=m.branch_revision AND r.revision=m.resource_revision
   AND d.branch_revision=m.branch_revision AND d.resource_revision=m.resource_revision;
 IF v_reference IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Private media denied';
 END IF;
 SELECT v_reference AS opaque_reference;
END//

CREATE PROCEDURE read_private_export(IN p_operation CHAR(32))
SQL SECURITY DEFINER
BEGIN
 DECLARE v_tenant CHAR(32) DEFAULT NULL;
 DECLARE v_actor CHAR(32) DEFAULT NULL;
 DECLARE v_enabled TINYINT DEFAULT NULL;
 DECLARE v_reference CHAR(64) DEFAULT NULL;
 SELECT tenant_id,actor_id,enabled INTO v_tenant,v_actor,v_enabled
 FROM test_account_binding WHERE sql_login=SUBSTRING_INDEX(USER(),'@',1);
 IF v_tenant IS NULL OR v_enabled <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No private export reader identity';
 END IF;
 SELECT e.manifest_reference INTO v_reference FROM export_manifests e
 JOIN decisions d ON d.operation_id=e.operation_id
 JOIN tenants t ON t.tenant_id=e.tenant_id
 JOIN branches b ON b.tenant_id=e.tenant_id AND b.branch_id=e.branch_id
 JOIN resources r ON r.tenant_id=e.tenant_id AND r.branch_id=e.branch_id
    AND r.resource_id=e.resource_id
 WHERE e.operation_id=p_operation AND e.tenant_id=v_tenant AND e.actor_id=v_actor
   AND d.approved=1 AND d.expires_at_utc>UTC_TIMESTAMP(6)
   AND t.state='active' AND t.access_epoch=e.tenant_epoch
   AND b.state='active' AND r.state='active' AND r.owner_actor=v_actor
   AND b.revision=e.branch_revision AND r.revision=e.resource_revision
   AND d.branch_revision=e.branch_revision AND d.resource_revision=e.resource_revision;
 IF v_reference IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Private export denied';
 END IF;
 SELECT v_reference AS opaque_reference;
END//
DELIMITER ;
