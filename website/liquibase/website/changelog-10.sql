--liquibase formatted sql

-- changeset kmaxi:casting-drop-reveal-setting
/*
  Other voters' comments are always hidden until a voter marks the character done, so a vote is
  never influenced by them. The per-round "always show" option is gone.
*/
ALTER TABLE casting_round DROP COLUMN reveal_comments;
--rollback ALTER TABLE casting_round ADD COLUMN reveal_comments enum('after_done','always') NOT NULL DEFAULT 'after_done' AFTER voting_closes_at;
