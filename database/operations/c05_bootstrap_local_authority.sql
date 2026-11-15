-- OFFLINE privileged operation; never grant EXECUTE/DDL to the API role.
-- Set @bootstrap_account and @bootstrap_tenant to BINARY(16), identifying an
-- existing mailbox-verified pending account created by the normal flow.
-- The selected person must still prove password + enroll/verify MFA by HTTP.
-- No default account, public bootstrap route, password or factor is created.
-- STOP the API while running this one-time operation. Use an isolated database.
DELIMITER $$
CREATE PROCEDURE c05_bootstrap_local_authority_once()
SQL SECURITY INVOKER
BEGIN
 DECLARE selected_count INT DEFAULT 0;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 START TRANSACTION;
 IF DATABASE()<>'crecisol_c05' OR @bootstrap_account IS NULL OR @bootstrap_tenant IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Explicit isolated bootstrap context required';
 END IF;
 SELECT COUNT(*) INTO selected_count FROM c05_identity_resource_grant
 WHERE permission_code='MEMBERSHIP_APPROVE';
 IF selected_count<>0 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Initial authority already provisioned';
 END IF;
 SELECT COUNT(*) INTO selected_count FROM c05_identity_account a
 JOIN c05_identity_membership m USING(account_id)
 JOIN c04_tenant_catalog t ON t.tenant_id=m.tenant_id
 WHERE a.account_id=@bootstrap_account AND m.tenant_id=@bootstrap_tenant
 AND a.account_state=0 AND a.mailbox_verified_at_utc IS NOT NULL
 AND m.membership_state=0 AND t.lifecycle_state=3;
 IF selected_count<>1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Verified pending identity required';
 END IF;
 UPDATE c05_identity_account SET account_state=1,updated_at_utc=UTC_TIMESTAMP(6)
 WHERE account_id=@bootstrap_account;
 UPDATE c05_identity_membership SET membership_state=1,role_code='LOCAL_APPROVER',
 approval_revision=approval_revision+1,approved_at_utc=UTC_TIMESTAMP(6),
 granted_at_utc=UTC_TIMESTAMP(6),expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 8 HOUR
 WHERE tenant_id=@bootstrap_tenant AND account_id=@bootstrap_account;
 INSERT INTO c05_identity_resource_grant VALUES
 (@bootstrap_tenant,UUID_TO_BIN(UUID(),0),@bootstrap_account,@bootstrap_tenant,
 NULL,2,'MEMBERSHIP_APPROVE',@bootstrap_account,UTC_TIMESTAMP(6),
 UTC_TIMESTAMP(6)+INTERVAL 8 HOUR,NULL,1);
 INSERT INTO c05_identity_audit VALUES
 (@bootstrap_tenant,UUID_TO_BIN(UUID(),0),@bootstrap_account,@bootstrap_account,
 'OFFLINE_INITIAL_AUTHORITY',1,UNHEX(SHA2(UUID(),256)),UTC_TIMESTAMP(6));
 COMMIT;
END$$
DELIMITER ;
CALL c05_bootstrap_local_authority_once();
DROP PROCEDURE c05_bootstrap_local_authority_once;
