DROP TRIGGER IF EXISTS c05_identity_membership_dates_before_update;
ALTER TABLE c05_identity_membership
  DROP CHECK ck_c05_membership_active_dates,
  DROP CHECK ck_c05_membership_profile,
  DROP COLUMN expires_at_utc,
  DROP COLUMN granted_at_utc,
  DROP COLUMN changed_at_utc,
  DROP COLUMN profile_kind;
