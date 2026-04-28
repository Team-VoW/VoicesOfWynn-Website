--liquibase formatted sql

-- changeset shady:1
-- validCheckSum: 9:c000fdd7e2e8c2967114bb519a29d57a

ALTER TABLE `npc`
    ADD `upvotes` int(11) NOT NULL DEFAULT 0,
    ADD `downvotes` int(11) NOT NULL DEFAULT 0 AFTER `upvotes`;

ALTER TABLE `recording`
    DROP `upvotes`,
    DROP `downvotes`;

ALTER TABLE `comment`
    ADD `npc_id` int(11) NULL,
    ADD CONSTRAINT `comment_ibfk_3` FOREIGN KEY (`npc_id`) REFERENCES `npc` (`npc_id`) ON DELETE SET NULL ON UPDATE CASCADE;

-- merge comments from individual recordings to NPCs
UPDATE comment
    SET npc_id = (SELECT npc_id FROM recording WHERE recording.recording_id = comment.recording_id);

-- delete now duplicated comments (manual pick from production database)
DELETE FROM `comment`
    WHERE `comment_id` IN (15,79,75,76,77,78);

ALTER TABLE `comment`
    DROP FOREIGN KEY `comment_ibfk_1`;

ALTER TABLE `comment`
    DROP `recording_id`;

-- not going to merge upvotes/downvotes
TRUNCATE TABLE `vote`;

ALTER TABLE `vote`
    ADD `npc_id` int(11) NULL AFTER `recording_id`,
    CHANGE `ip` `voter` varchar(64) COLLATE 'ascii_general_ci' NOT NULL AFTER `npc_id`,
    ADD FOREIGN KEY (`npc_id`) REFERENCES `npc` (`npc_id`) ON DELETE SET NULL ON UPDATE CASCADE;

ALTER TABLE `vote`
    ADD UNIQUE `npc_id_voter` (`npc_id`, `voter`);

ALTER TABLE `vote`
    DROP FOREIGN KEY `vote_ibfk_1`;

ALTER TABLE `vote`
    DROP `recording_id`;