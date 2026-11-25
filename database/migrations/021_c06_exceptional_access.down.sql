-- Only for a disposable local schema; preserve evidence before any real rollback.
DROP TABLE IF EXISTS c06_exception_audit;
DROP TABLE IF EXISTS c06_exception_close;
DROP TABLE IF EXISTS c06_exception_approval;
DROP TABLE IF EXISTS c06_exception_request;
DROP TABLE IF EXISTS c06_exception_entitlement;
