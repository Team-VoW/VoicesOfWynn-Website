--liquibase formatted sql

-- changeset shady:2

ALTER TABLE `quest`
    ADD `writer` int(11) NULL DEFAULT NULL COMMENT 'User account ID of the writer that created the script for this quest' AFTER `degenerated_name`;

ALTER TABLE `quest`
    ADD INDEX `writer` (`writer`);

ALTER TABLE `quest` ADD FOREIGN KEY (`writer`) REFERENCES `user` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE;

ALTER TABLE `npc_quest`
    ADD `editor` int(11) NULL DEFAULT NULL COMMENT 'User account ID of the sound editor that edited the sound files of given NPC for given quest';

ALTER TABLE `npc_quest`
    ADD INDEX `editor` (`editor`);

ALTER TABLE `npc_quest` ADD FOREIGN KEY (`editor`) REFERENCES `user` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE;