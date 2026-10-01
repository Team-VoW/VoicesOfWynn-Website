--liquibase formatted sql

-- changeset kmaxi:casting-character-ccc-details
ALTER TABLE casting_character
  ADD COLUMN audition_lines text DEFAULT NULL,
  ADD COLUMN image_url varchar(1000) DEFAULT NULL;
--rollback ALTER TABLE casting_character DROP COLUMN image_url, DROP COLUMN audition_lines;
