DROP PROCEDURE IF EXISTS c06_privacy_request_rollback;
DELIMITER $$
CREATE PROCEDURE c06_privacy_request_rollback()
BEGIN
 IF EXISTS(SELECT 1 FROM c06_privacy_request) OR EXISTS(SELECT 1 FROM c06_privacy_request_intent)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy intake not empty; preserve history'; END IF;
 DROP TABLE c06_privacy_request;
 DROP TABLE c06_privacy_request_intent;
END$$
DELIMITER ;
CALL c06_privacy_request_rollback();
DROP PROCEDURE c06_privacy_request_rollback;
