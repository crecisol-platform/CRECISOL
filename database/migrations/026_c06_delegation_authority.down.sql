-- Reverse only empty candidate tables under exclusive isolated maintenance.
DROP PROCEDURE IF EXISTS c06_delegation_authority_rollback;
DELIMITER $$
CREATE PROCEDURE c06_delegation_authority_rollback()
BEGIN
    IF EXISTS(SELECT 1 FROM c06_delegation_authority)
    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 issuance provenance not empty'; END IF;
    DROP TABLE c06_delegation_authority;
END$$
DELIMITER ;
CALL c06_delegation_authority_rollback();
DROP PROCEDURE c06_delegation_authority_rollback;
