--liquibase formatted sql

-- changeset kmaxi:4
/*
  The comment table has never carried a timestamp, so comments cannot be dated in the UI.
  The column is added as NULL and only then given a default, which leaves existing rows NULL
  instead of claiming every historical comment was posted at migration time. The API serves
  those as an unknown date.
*/
ALTER TABLE `comment`
    ADD COLUMN `created_at` datetime NULL DEFAULT NULL;
ALTER TABLE `comment`
    MODIFY COLUMN `created_at` datetime NULL DEFAULT CURRENT_TIMESTAMP;
CREATE INDEX `comment_npc_created` ON `comment` (`npc_id`, `created_at`);
--rollback DROP INDEX `comment_npc_created` ON `comment`;
--rollback ALTER TABLE `comment` DROP COLUMN `created_at`;
