--liquibase formatted sql

-- changeset kmaxi:casting-comments-without-votes
/*
  A casting_vote row is now a voter's take on one audition: a pick, a comment, or both. Staff can
  comment on auditions they did not pick, and withdrawing a pick keeps the comment. A row is only
  removed once it is neither picked nor commented. Existing rows were all picks.
*/
ALTER TABLE casting_vote
    ADD COLUMN picked tinyint(1) NOT NULL DEFAULT 1 COMMENT '0 = comment only, the voter did not pick this audition' AFTER user_id;
--rollback DELETE FROM casting_vote WHERE picked = 0;
--rollback ALTER TABLE casting_vote DROP COLUMN picked;
