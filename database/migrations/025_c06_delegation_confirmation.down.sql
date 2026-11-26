-- Empty isolated schema rollback only; never erase confirmation evidence.
DROP PROCEDURE IF EXISTS c06_delegation_confirmation_rollback;
DELIMITER $$
CREATE PROCEDURE c06_delegation_confirmation_rollback()
BEGIN
    IF EXISTS(SELECT 1 FROM c06_delegation_confirmation) OR
       EXISTS(SELECT 1 FROM c06_delegation_lab_entitlement)
    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 confirmation not empty; preserve history'; END IF;
    DROP TABLE c06_delegation_confirmation;
    DROP TABLE c06_delegation_lab_entitlement;
END$$
DELIMITER ;
CALL c06_delegation_confirmation_rollback();
DROP PROCEDURE c06_delegation_confirmation_rollback;
