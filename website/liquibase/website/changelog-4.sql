--liquibase formatted sql

-- changeset kmaxi:3

ALTER TABLE `user`
    ADD `picture_type` enum('default','discord','manual') NOT NULL DEFAULT 'default' COMMENT 'Source of the profile picture; default means use avatars/default.png.' AFTER `picture`;

UPDATE `user`
    SET `picture_type` = 'manual'
    WHERE `picture` <> 'default.png';
