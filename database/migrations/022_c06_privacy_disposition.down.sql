-- Removes only the local maintenance entry point. Already disposed
-- payloads cannot be restored logically; preserve authorization/audit evidence.
DROP PROCEDURE IF EXISTS c06_dispose_closed_sensitive_payloads;
