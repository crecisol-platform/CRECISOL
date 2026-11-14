-- C05-09: tenant- and actor-scoped PRIVATE profiles and media pointers.
-- DISPOSABLE MySQL8.4 integration fixture ONLY; never a production migration.
-- Depends on C05-05 sessions + C05-06 credentials/mailboxes fixtures.
-- No image bytes/BLOB/base64, user PII plaintext, public/signed URL,
-- Cloudflare token, password, email or upload bytes in the DB.
CREATE TABLE c05_09_media_manifest (
 tenant_id BINARY(16) NOT NULL,
 actor_id BINARY(16) NOT NULL,
 media_id BINARY(16) NOT NULL,
 object_key VARCHAR(192) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 content_type VARCHAR(24) CHARACTER SET ascii NOT NULL,
 byte_length BIGINT UNSIGNED NOT NULL,
 content_sha256 BINARY(32) NOT NULL,
 state VARCHAR(9) CHARACTER SET ascii NOT NULL,
 scan_state VARCHAR(8) CHARACTER SET ascii NOT NULL,
 is_private TINYINT(1) NOT NULL,
 checked_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (tenant_id,media_id),
 UNIQUE KEY uq_c05_09_media_owner (tenant_id,actor_id,media_id),
 CONSTRAINT fk_c05_09_manifest_subject FOREIGN KEY (tenant_id,actor_id)
   REFERENCES c05_05_subjects(tenant_id,actor_id)
   ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT ck_c05_09_media_size CHECK (byte_length BETWEEN 128 AND 2097152),
 CONSTRAINT ck_c05_09_media_type CHECK (content_type='image/webp'),
 CONSTRAINT ck_c05_09_media_state CHECK (state IN ('PENDING','VERIFIED','REVOKED')),
 CONSTRAINT ck_c05_09_media_scan CHECK (scan_state IN ('PENDING','CLEAN','BLOCKED')),
 CONSTRAINT ck_c05_09_media_privacy CHECK (is_private=1)
) ENGINE=InnoDB;

CREATE TABLE c05_09_private_profiles (
 tenant_id BINARY(16) NOT NULL,
 actor_id BINARY(16) NOT NULL,
 profile_revision BIGINT UNSIGNED NOT NULL,
 pii_ciphertext VARBINARY(512) NULL,
 photo_id BINARY(16) NULL,
 changed_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (tenant_id,actor_id),
 CONSTRAINT fk_c05_09_profile_subject FOREIGN KEY (tenant_id,actor_id)
   REFERENCES c05_05_subjects(tenant_id,actor_id)
   ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_c05_09_profile_photo FOREIGN KEY (tenant_id,actor_id,photo_id)
   REFERENCES c05_09_media_manifest(tenant_id,actor_id,media_id)
   ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT ck_c05_09_revision CHECK(profile_revision>0),
 CONSTRAINT ck_c05_09_envelope CHECK(
   pii_ciphertext IS NULL OR
   (OCTET_LENGTH(pii_ciphertext) BETWEEN 31 AND 512 AND
    ORD(SUBSTRING(pii_ciphertext,1,1))=1)),
 CONSTRAINT ck_c05_09_erase CHECK(
   pii_ciphertext IS NOT NULL OR photo_id IS NULL)
) ENGINE=InnoDB;

CREATE TABLE c05_09_profile_audit (
 audit_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 tenant_id BINARY(16) NOT NULL,
 actor_id BINARY(16) NOT NULL,
 revision BIGINT UNSIGNED NOT NULL,
 event_type VARCHAR(12) CHARACTER SET ascii NOT NULL,
 event_utc DATETIME(6) NOT NULL,
 CONSTRAINT fk_c05_09_audit_actor FOREIGN KEY (tenant_id,actor_id)
   REFERENCES c05_05_subjects(tenant_id,actor_id)
   ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT ck_c05_09_audit_type CHECK(event_type IN ('CREATED','UPDATED','ERASED'))
) ENGINE=InnoDB;
DELIMITER $$

CREATE TRIGGER c05_09_audit_no_update
BEFORE UPDATE ON c05_09_profile_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-09 audit immutable'; END$$
CREATE TRIGGER c05_09_audit_no_delete
BEFORE DELETE ON c05_09_profile_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-09 audit immutable'; END$$

-- Profile operations independently revalidate (tenant,actor,link,account,
-- credential,membership,tenant catalog,host) under a single InnoDB transaction.
-- p_tenant/p_actor come ONLY from freshly verified C05-04 backend evidence;
-- there is no real identity/session endpoint using this test fixture.

CREATE PROCEDURE c05_09_read_self(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_link BINARY(16),
 IN p_account BIGINT UNSIGNED, IN p_credential BIGINT UNSIGNED,
 IN p_membership BIGINT UNSIGNED, IN p_tenant_revision BIGINT UNSIGNED,
 IN p_host BIGINT UNSIGNED
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOLEAN DEFAULT FALSE;
 DECLARE v_link BINARY(16);
 DECLARE v_a BIGINT UNSIGNED; DECLARE v_c BIGINT UNSIGNED;
 DECLARE v_m BIGINT UNSIGNED; DECLARE v_t BIGINT UNSIGNED;
 DECLARE v_h BIGINT UNSIGNED;
 DECLARE v_account_active TINYINT; DECLARE v_credential_active TINYINT;
 DECLARE v_member_active TINYINT; DECLARE v_host_active TINYINT;
 DECLARE v_tenant_active TINYINT; DECLARE v_expires DATETIME(6);
 DECLARE v_global BIGINT UNSIGNED; DECLARE v_global_active TINYINT;
 DECLARE v_realm VARCHAR(12);
 DECLARE v_mail_verified TINYINT;
 DECLARE v_version BIGINT UNSIGNED;
 DECLARE v_cipher VARBINARY(512); DECLARE v_photo BINARY(16);
 DECLARE v_photo_state VARCHAR(9); DECLARE v_photo_scan VARCHAR(8);
 DECLARE v_photo_private TINYINT;
 DECLARE v_now DATETIME(6);
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT link_id,account_revision,credential_revision,membership_revision,
        tenant_revision,host_revision,account_active,credential_active,
        membership_active,host_active,tenant_active,member_expires_utc
 INTO v_link,v_a,v_c,v_m,v_t,v_h,v_account_active,v_credential_active,
      v_member_active,v_host_active,v_tenant_active,v_expires
 FROM c05_05_subjects WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_link<>p_link OR v_a<>p_account OR v_c<>p_credential OR
    v_m<>p_membership OR v_t<>p_tenant_revision OR v_h<>p_host OR
    v_account_active<>1 OR v_credential_active<>1 OR v_member_active<>1 OR
    v_host_active<>1 OR v_tenant_active<>1 OR v_expires<=v_now THEN
   ROLLBACK; SELECT 'DENIED' AS result,NULL AS revision,NULL AS envelope,NULL AS photo_id;
   LEAVE proc;
 END IF;
 SELECT credential_revision,credential_active,realm
 INTO v_global,v_global_active,v_realm FROM c05_06_credentials
 WHERE actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_global<>v_c OR v_global_active<>1 OR v_realm<>'TENANT' THEN
   ROLLBACK; SELECT 'DENIED' AS result,NULL AS revision,NULL AS envelope,NULL AS photo_id;
   LEAVE proc;
 END IF;
 SELECT verified INTO v_mail_verified FROM c05_06_mailboxes
 WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_mail_verified<>1 THEN
   ROLLBACK; SELECT 'DENIED' AS result,NULL AS revision,NULL AS envelope,NULL AS photo_id;
   LEAVE proc;
 END IF;
 SELECT profile_revision,pii_ciphertext,photo_id
 INTO v_version,v_cipher,v_photo FROM c05_09_private_profiles
 WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_cipher IS NULL THEN
   COMMIT; SELECT 'NONE' AS result,NULL AS revision,NULL AS envelope,NULL AS photo_id;
   LEAVE proc;
 END IF;
 IF v_photo IS NOT NULL THEN
   SELECT state,scan_state,is_private
   INTO v_photo_state,v_photo_scan,v_photo_private
   FROM c05_09_media_manifest
   WHERE tenant_id=p_tenant AND actor_id=p_actor AND media_id=v_photo FOR UPDATE;
   IF v_missing OR v_photo_state<>'VERIFIED' OR v_photo_scan<>'CLEAN' OR
      v_photo_private<>1 THEN SET v_photo=NULL; END IF;
 END IF;
 COMMIT;
 SELECT 'FOUND' AS result,v_version AS revision,v_cipher AS envelope,
        v_photo AS photo_id;
END$$

CREATE PROCEDURE c05_09_save_self(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_link BINARY(16),
 IN p_account BIGINT UNSIGNED, IN p_credential BIGINT UNSIGNED,
 IN p_membership BIGINT UNSIGNED, IN p_tenant_revision BIGINT UNSIGNED,
 IN p_host BIGINT UNSIGNED, IN p_expected BIGINT UNSIGNED,
 IN p_cipher VARBINARY(512), IN p_photo BINARY(16)
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOLEAN DEFAULT FALSE;
 DECLARE v_link BINARY(16);
 DECLARE v_a BIGINT UNSIGNED; DECLARE v_c BIGINT UNSIGNED;
 DECLARE v_m BIGINT UNSIGNED; DECLARE v_t BIGINT UNSIGNED;
 DECLARE v_h BIGINT UNSIGNED;
 DECLARE v_account_active TINYINT; DECLARE v_credential_active TINYINT;
 DECLARE v_member_active TINYINT; DECLARE v_host_active TINYINT;
 DECLARE v_tenant_active TINYINT; DECLARE v_expires DATETIME(6);
 DECLARE v_global BIGINT UNSIGNED; DECLARE v_global_active TINYINT;
 DECLARE v_realm VARCHAR(12); DECLARE v_mail_verified TINYINT;
 DECLARE v_version BIGINT UNSIGNED; DECLARE v_new_profile BOOLEAN DEFAULT FALSE;
 DECLARE v_key VARCHAR(192);
 DECLARE v_mime VARCHAR(24); DECLARE v_bytes BIGINT UNSIGNED;
 DECLARE v_photo_state VARCHAR(9); DECLARE v_scan VARCHAR(8);
 DECLARE v_is_private TINYINT; DECLARE v_now DATETIME(6);
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 IF p_cipher IS NULL OR OCTET_LENGTH(p_cipher) NOT BETWEEN 31 AND 512 OR
    ORD(SUBSTRING(p_cipher,1,1))<>1 THEN
   SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT link_id,account_revision,credential_revision,membership_revision,
        tenant_revision,host_revision,account_active,credential_active,
        membership_active,host_active,tenant_active,member_expires_utc
 INTO v_link,v_a,v_c,v_m,v_t,v_h,v_account_active,v_credential_active,
      v_member_active,v_host_active,v_tenant_active,v_expires
 FROM c05_05_subjects WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_link<>p_link OR v_a<>p_account OR v_c<>p_credential OR
    v_m<>p_membership OR v_t<>p_tenant_revision OR v_h<>p_host OR
    v_account_active<>1 OR v_credential_active<>1 OR v_member_active<>1 OR
    v_host_active<>1 OR v_tenant_active<>1 OR v_expires<=v_now THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SELECT credential_revision,credential_active,realm
 INTO v_global,v_global_active,v_realm FROM c05_06_credentials
 WHERE actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_global<>v_c OR v_global_active<>1 OR v_realm<>'TENANT' THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SELECT verified INTO v_mail_verified FROM c05_06_mailboxes
 WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_mail_verified<>1 THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SET v_missing=FALSE;
 SELECT profile_revision INTO v_version FROM c05_09_private_profiles
 WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 SET v_new_profile=v_missing;
 SET v_missing=FALSE;
 IF p_photo IS NOT NULL THEN
   SELECT object_key,content_type,byte_length,state,scan_state,is_private
   INTO v_key,v_mime,v_bytes,v_photo_state,v_scan,v_is_private
   FROM c05_09_media_manifest
   WHERE tenant_id=p_tenant AND actor_id=p_actor AND media_id=p_photo FOR UPDATE;
   IF v_missing OR v_key<>CONCAT('profiles/',LOWER(HEX(p_tenant)),'/',
        LOWER(HEX(p_actor)),'/',LOWER(HEX(p_photo)),'.webp') OR
      v_mime<>'image/webp' OR v_bytes NOT BETWEEN 128 AND 2097152 OR
      v_photo_state<>'VERIFIED' OR v_scan<>'CLEAN' OR v_is_private<>1 THEN
     ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
   END IF;
 END IF;
 IF v_new_profile THEN
   IF p_expected<>0 THEN
     ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
   END IF;
   INSERT INTO c05_09_private_profiles
       (tenant_id,actor_id,profile_revision,pii_ciphertext,photo_id,changed_utc)
   VALUES (p_tenant,p_actor,1,p_cipher,p_photo,v_now);
   INSERT INTO c05_09_profile_audit
       (tenant_id,actor_id,revision,event_type,event_utc)
   VALUES (p_tenant,p_actor,1,'CREATED',v_now);
   COMMIT; SELECT 'SAVED' AS result; LEAVE proc;
 END IF;
 IF v_version<>p_expected OR v_version>=9223372036854775806 THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 UPDATE c05_09_private_profiles SET pii_ciphertext=p_cipher,
     photo_id=p_photo,profile_revision=profile_revision+1,changed_utc=v_now
 WHERE tenant_id=p_tenant AND actor_id=p_actor AND profile_revision=p_expected;
 INSERT INTO c05_09_profile_audit
     (tenant_id,actor_id,revision,event_type,event_utc)
 VALUES (p_tenant,p_actor,p_expected+1,'UPDATED',v_now);
 COMMIT; SELECT 'SAVED' AS result;
END$$

CREATE PROCEDURE c05_09_erase_self(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_link BINARY(16),
 IN p_account BIGINT UNSIGNED, IN p_credential BIGINT UNSIGNED,
 IN p_membership BIGINT UNSIGNED, IN p_tenant_revision BIGINT UNSIGNED,
 IN p_host BIGINT UNSIGNED, IN p_expected BIGINT UNSIGNED
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOLEAN DEFAULT FALSE;
 DECLARE v_link BINARY(16);
 DECLARE v_a BIGINT UNSIGNED; DECLARE v_c BIGINT UNSIGNED;
 DECLARE v_m BIGINT UNSIGNED; DECLARE v_t BIGINT UNSIGNED;
 DECLARE v_h BIGINT UNSIGNED;
 DECLARE v_account_active TINYINT; DECLARE v_credential_active TINYINT;
 DECLARE v_member_active TINYINT; DECLARE v_host_active TINYINT;
 DECLARE v_tenant_active TINYINT; DECLARE v_expires DATETIME(6);
 DECLARE v_global BIGINT UNSIGNED; DECLARE v_global_active TINYINT;
 DECLARE v_realm VARCHAR(12); DECLARE v_mail_verified TINYINT;
 DECLARE v_version BIGINT UNSIGNED; DECLARE v_now DATETIME(6);
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 IF p_expected<1 THEN SELECT 'DENIED' AS result; LEAVE proc; END IF;
 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT link_id,account_revision,credential_revision,membership_revision,
        tenant_revision,host_revision,account_active,credential_active,
        membership_active,host_active,tenant_active,member_expires_utc
 INTO v_link,v_a,v_c,v_m,v_t,v_h,v_account_active,v_credential_active,
      v_member_active,v_host_active,v_tenant_active,v_expires
 FROM c05_05_subjects WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_link<>p_link OR v_a<>p_account OR v_c<>p_credential OR
    v_m<>p_membership OR v_t<>p_tenant_revision OR v_h<>p_host OR
    v_account_active<>1 OR v_credential_active<>1 OR v_member_active<>1 OR
    v_host_active<>1 OR v_tenant_active<>1 OR v_expires<=v_now THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SELECT credential_revision,credential_active,realm
 INTO v_global,v_global_active,v_realm FROM c05_06_credentials
 WHERE actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_global<>v_c OR v_global_active<>1 OR v_realm<>'TENANT' THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SELECT verified INTO v_mail_verified FROM c05_06_mailboxes
 WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_mail_verified<>1 THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 SELECT profile_revision INTO v_version FROM c05_09_private_profiles
 WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_version<>p_expected OR
    v_version>=9223372036854775806 THEN
   ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
 END IF;
 UPDATE c05_09_private_profiles SET pii_ciphertext=NULL,photo_id=NULL,
     profile_revision=profile_revision+1,changed_utc=v_now
 WHERE tenant_id=p_tenant AND actor_id=p_actor AND profile_revision=p_expected;
 INSERT INTO c05_09_profile_audit
     (tenant_id,actor_id,revision,event_type,event_utc)
 VALUES (p_tenant,p_actor,p_expected+1,'ERASED',v_now);
 COMMIT; SELECT 'ERASED' AS result;
END$$
DELIMITER ;
