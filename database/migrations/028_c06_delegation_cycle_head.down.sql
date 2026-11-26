-- C06-11 candidate reversal. Exclusive maintenance only; DDL commits per
-- statement. Refuse before changing any product table if a second generation
-- exists or the head cannot be reconstructed exactly in the old schema.
-- Never truncate/delete issuance, revocation or provenance history.
DELIMITER $$
CREATE PROCEDURE c06_cycle_assert_representable()
BEGIN
 IF EXISTS(SELECT 1 FROM c06_delegation_grant WHERE revision<>1)
 OR EXISTS(SELECT 1 FROM c06_delegation_revocation WHERE expected_revision<>1 OR revision<>2)
 OR (SELECT COUNT(*) FROM c06_delegation_cycle_head)<>(SELECT COUNT(*) FROM c06_delegation_grant)
 OR EXISTS(
   SELECT 1 FROM c06_delegation_grant g
   LEFT JOIN c06_delegation_revocation v ON v.tenant_id=g.tenant_id AND v.grant_id=g.grant_id
   LEFT JOIN c06_delegation_cycle_head h
     ON h.tenant_id=g.tenant_id AND h.subject_membership_id=g.subject_membership_id
     AND h.branch_id=g.branch_id AND h.resource_id=g.resource_id AND h.action_id=g.action_id
   WHERE h.grant_id IS NULL OR h.grant_id<>g.grant_id OR h.role_id<>g.role_id
     OR h.issuance_revision<>g.revision OR h.revision<>COALESCE(v.revision,g.revision)
     OR NOT(h.terminal_at_utc<=>v.revoked_at_utc)
 )
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 lifecycle not representable in previous ledger; preserve history';
 END IF;
END$$
DELIMITER ;
CALL c06_cycle_assert_representable();
DROP PROCEDURE c06_cycle_assert_representable;

DROP TRIGGER trg_c06_dv_cycle_head;
DROP TRIGGER trg_c06_dg_cycle_head;
DROP TABLE c06_delegation_cycle_head;

ALTER TABLE c06_delegation_grant
 ADD UNIQUE KEY uq_c06_dg_scope(tenant_id,subject_membership_id,branch_id,resource_id,action_id),
 DROP CHECK ck_c06_dg_cycle_revision,
 ADD CONSTRAINT ck_c06_dg_revision CHECK(
   revision=1 AND role_revision>0 AND subject_revision>0 AND issuer_revision>0 AND ceiling_revision>0);
ALTER TABLE c06_delegation_grant
 DROP INDEX uq_c06_dg_head_ref,
 DROP INDEX uq_c06_dg_generation;
ALTER TABLE c06_delegation_revocation
 DROP CHECK ck_c06_dv_cycle_revision,
 ADD CONSTRAINT ck_c06_dv_revision CHECK(expected_revision=1 AND revision=2 AND actor_revision>0 AND ceiling_revision>0);

-- A refused CALL leaves only the helper procedure; keep the schema in
-- maintenance and inspect it. No destructive retry or --force is allowed.
