-- Only an EMPTY isolated candidate may be reverted, in exclusive maintenance.
-- Do not erase authorization history. DDL is not a transactional rollback.
DROP PROCEDURE IF EXISTS c06_delegation_drop_empty;
DELIMITER $$
CREATE PROCEDURE c06_delegation_drop_empty()
BEGIN
 IF EXISTS(SELECT 1 FROM c06_delegation_revocation) OR EXISTS(SELECT 1 FROM c06_delegation_grant)
 OR EXISTS(SELECT 1 FROM c06_delegation_ceiling) OR EXISTS(SELECT 1 FROM c06_delegation_role)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 delegation ledger not empty; preserve history'; END IF;
 DROP TABLE c06_delegation_revocation;
 DROP TABLE c06_delegation_grant;
 DROP TABLE c06_delegation_ceiling;
 DROP TABLE c06_delegation_role;
END$$
DELIMITER ;
CALL c06_delegation_drop_empty();
DROP PROCEDURE c06_delegation_drop_empty;
