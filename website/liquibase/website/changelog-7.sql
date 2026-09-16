--liquibase formatted sql

-- changeset kmaxi:5
/*
  Instagram and GitHub join the social handles a contributor can publish on the cast page.
  Both are stored as bare handles, like twitter and castingcallclub, and the lengths match what
  each platform allows: 30 characters for an Instagram username, 39 for a GitHub one.
  The PHP site is deliberately left untouched, so it neither reads nor writes these columns;
  only the Vue frontend and the API expose them.
*/
ALTER TABLE `user`
    ADD COLUMN `instagram` varchar(30) DEFAULT NULL COMMENT 'Instagram username, without the leading @';
ALTER TABLE `user`
    ADD COLUMN `github` varchar(39) DEFAULT NULL COMMENT 'GitHub username';
--rollback ALTER TABLE `user` DROP COLUMN `github`;
--rollback ALTER TABLE `user` DROP COLUMN `instagram`;
