-- C05 operational login directory metadata.
-- This migration adds only server-owned membership profile/lifecycle dates.
-- Profile is display context, never an authorization grant.
ALTER TABLE c05_identity_membership
  ADD COLUMN profile_kind TINYINT UNSIGNED NOT NULL DEFAULT 3 AFTER account_id,
  ADD COLUMN changed_at_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
    AFTER approval_revision,
  ADD COLUMN granted_at_utc DATETIME(6) NULL AFTER approved_at_utc,
  ADD COLUMN expires_at_utc DATETIME(6) NULL AFTER granted_at_utc;

UPDATE c05_identity_membership
   SET changed_at_utc=COALESCE(approved_at_utc,created_at_utc),
       granted_at_utc=IF(membership_state=1,
                         COALESCE(approved_at_utc,created_at_utc),NULL),
       expires_at_utc=IF(membership_state=1,'9999-12-31 23:59:59.999999',NULL);

ALTER TABLE c05_identity_membership
  ADD CONSTRAINT ck_c05_membership_profile CHECK (profile_kind IN (1,2,3)),
  ADD CONSTRAINT ck_c05_membership_active_dates CHECK (
    membership_state<>1 OR
    (granted_at_utc IS NOT NULL AND expires_at_utc IS NOT NULL AND
     expires_at_utc>granted_at_utc)
  );

DELIMITER $$
CREATE TRIGGER c05_identity_membership_dates_before_update
BEFORE UPDATE ON c05_identity_membership FOR EACH ROW
BEGIN
  IF NEW.membership_state<>OLD.membership_state OR
     NEW.profile_kind<>OLD.profile_kind OR
     NEW.role_code<>OLD.role_code OR
     NEW.approval_revision<>OLD.approval_revision THEN
    SET NEW.changed_at_utc=UTC_TIMESTAMP(6);
  END IF;
  IF NEW.membership_state=1 THEN
    SET NEW.granted_at_utc=COALESCE(NEW.granted_at_utc,UTC_TIMESTAMP(6));
    SET NEW.expires_at_utc=COALESCE(NEW.expires_at_utc,'9999-12-31 23:59:59.999999');
  END IF;
END$$
DELIMITER ;
