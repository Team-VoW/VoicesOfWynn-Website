--liquibase formatted sql

-- changeset kmaxi:1

USE api;

SET NAMES utf8mb4;

-- Mod bootup configuration, moved out of website/Models/Api/VersionChecker/VersionConfig/version.ini
-- so it can be edited from the staff Admin page without redeploying the PHP image.
CREATE TABLE mod_release (
  id                          tinyint(1)   NOT NULL DEFAULT 1,
  latest_version              varchar(32)  NOT NULL,
  update_notification_version varchar(32)  NOT NULL,
  kill_switch_version         varchar(32)  NOT NULL,
  download_url                varchar(511) NOT NULL,
  changelog_url               varchar(511) NOT NULL,
  audio_base_url              varchar(511) NOT NULL,
  audio_mirror_urls           text         NOT NULL COMMENT 'JSON array of strings',
  updated_at                  timestamp    NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  updated_by                  int(11)      NULL COMMENT 'website.user.user_id; no FK, different schema',
  PRIMARY KEY (id),
  CONSTRAINT mod_release_single_row CHECK (id = 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Moved out of website/Models/Api/MessageBroadcast/BroadcastConfig/broadcast.ini.
-- Windows are stored in UTC; the ini-based loader compared against server-local time.
CREATE TABLE mod_broadcast (
  broadcast_id int(11)      NOT NULL AUTO_INCREMENT,
  content      varchar(511) NOT NULL,
  active_from  datetime     NOT NULL,
  active_until datetime     NOT NULL,
  created_at   timestamp    NOT NULL DEFAULT current_timestamp(),
  created_by   int(11)      NULL COMMENT 'website.user.user_id; no FK, different schema',
  PRIMARY KEY (broadcast_id),
  KEY window_idx (active_from, active_until)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Moved out of website/Models/Api/FunFacts/Library/*.txt. slug keeps the old filename so a
-- fact can still be traced back to the file it came from.
CREATE TABLE fun_fact (
  fun_fact_id int(11)     NOT NULL AUTO_INCREMENT,
  slug        varchar(64) NOT NULL,
  content     text        NOT NULL,
  active      tinyint(1)  NOT NULL DEFAULT 1,
  created_at  timestamp   NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (fun_fact_id),
  UNIQUE KEY slug (slug)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- The old aggregator appended unconditionally, so a second run over an already-processed day
-- added a second row for it (counting 0 pings, since the first run had deleted them). Collapse
-- any such pairs into one row before the unique key can be added, or this changeset fails on any
-- database where the job was ever run twice.
UPDATE daily d
JOIN (
    SELECT MIN(daily_id) AS keep_id, SUM(bootups) AS total
    FROM daily GROUP BY date HAVING COUNT(*) > 1
) dup ON d.daily_id = dup.keep_id
SET d.bootups = dup.total;

DELETE d FROM daily d
JOIN (
    SELECT date, MIN(daily_id) AS keep_id
    FROM daily GROUP BY date HAVING COUNT(*) > 1
) dup ON d.date = dup.date AND d.daily_id <> dup.keep_id;

-- Aggregation writes one row per day and must be safe to re-run; without this a second run
-- silently appended duplicate rows, which the daily-usage chart then double-counted.
ALTER TABLE daily ADD UNIQUE KEY date_unique (date);

-- The bootup throttle has always looked up the newest ping per uuid and per ip, and the
-- aggregator scans by day, none of which had an index.
ALTER TABLE ping
  ADD KEY uuid_time (uuid, time),
  ADD KEY ip_time (ip, time),
  ADD KEY time_idx (time);

--rollback ALTER TABLE ping DROP KEY uuid_time, DROP KEY ip_time, DROP KEY time_idx;
--rollback ALTER TABLE daily DROP KEY date_unique;
--rollback DROP TABLE fun_fact;
--rollback DROP TABLE mod_broadcast;
--rollback DROP TABLE mod_release;

-- changeset kmaxi:2

USE api;

SET NAMES utf8mb4;

INSERT INTO mod_release (
  id, latest_version, update_notification_version, kill_switch_version,
  download_url, changelog_url, audio_base_url, audio_mirror_urls
) VALUES (
  1, '2.0.3', '2.0.2', '0',
  'https://cdn.modrinth.com/data/Hn8Ot3qH/versions/hIAKbbxt/Voices-of-Wynn-fabric-2.0.3-fabric%2BMC-1.21.11.jar',
  'https://modrinth.com/mod/vow',
  'https://cdn.jsdelivr.net/gh/Team-VoW/WynncraftVoiceProject@main/sounds/',
  '["https://cdn.jsdelivr.net/gh/Team-VoW/WynncraftVoiceProject@main/sounds/"]'
);

INSERT INTO mod_broadcast (content, active_from, active_until) VALUES
('We are casting the roles of all Citizen NPCs in Western Gavel! Join the Voices of Wynn Discord server to audition. Deadline is 27th October. https://discord.gg/C8npHFXc3b', '2025-10-13 00:00:00', '2025-10-27 00:00:00');

INSERT INTO fun_fact (slug, content) VALUES
('107_takes', 'One of the Talking Mushroom lines took 107 takes for the voice actor to be satisfied with it! Talk about nitpicky!'),
('big_modfile', 'The mod used to be around 800 MB in size, because it used to contain all the voice lines, before we remade it to download voice lines on-demand from our servers. Because of such size, we couldn''t host it on Modridth.'),
('blanket', 'Some lines were recorded under a blanket.'),
('chat_gpt_stupid', 'In 2025, one confused user asked us in our support forum what key do they need to press in order to hear their friends talk. A few messages later we discovered, that ChatGPT told them that they need Voices of Wynn for in-game voicechat feature.'),
('discord_boycott', 'Our team''s infrastructure is heavily centered around Discord. When Discord started messing around with ID verification, we did a poll on moving to Matrix if things went bad. Most of the polling users were interested.'),
('discord_channels', 'There are more staff text channels (not counting temporary application channels) on our Discord server, than public text channels.'),
('drunk_seaskipper', 'Biscuit, the person voicing Seaskipper Captain got drunk before recording some of his lines. It took him many tries to hit the correct keys nad name his files as instructed.'),
('first_quest', 'The first quest that has been finished is Creeper Infiltration.'),
('fruma_dialogue_system', 'The new dialogue system that was introduced along with Fruma required kmaxi to remake the whole dialogue detection part of the mod''s code. And it was huge pain.'),
('honeypot', 'We created a special honeypot channel on our Discord server to try and catch scammers and bots. It (mostly) doesn''t work.'),
('imaxe_helping', 'The Wynncraft Content Team Manager – Imaxe – joined our staff team before Fruma was publically released and helped us pick the best voice actors for the NPCs whose lore he created.'),
('italy_mans_roles', 'In case it wasn''t obvious, Excavator Admin Uci is voiced by the same voice actor as the Talking Mushroom. A bit of a stretch for his vocal range.'),
('kmaxi_family_voicing', 'Kmaxi got quite a few people from his surroundings to voice an NPC in this mod. His sister, father and girlfriend voiced one NPC each.'),
('kmaxi_maxie', 'Kmaxi tried to get the role of Maxie, because it''s a domestic version of his real name. He failed.'),
('lanu_hangover', 'The voice actress of Lanu had to delay submitting her voicelines for the Fruma update, because she had a birthday hangover and couldn''t get her voice to work properly.'),
('legacy_comments', 'In the older version of our website, you could rate and comment on individual recordings, not the whole NPCs. It didn''t make much sense and one person commented the same thing on all voicelines of a certain NPC.'),
('lost_fun_facts', 'We had maybe 50 more fun facts, but they weren''t commited to our GitHub repoitory and when we moved our hosting providers, we forgot to save them and lost them.'),
('master_actor', 'Most people think that there are 4 tiers of voice actor roles on our Discord server: Beginner, Advanced, Skilled, and Expert. However, there is a fifth hidden one: Master, which only has 3 members to date.'),
('no_mixed_feelings_pls', 'If you find a bug with the lines in Mixed Feelings, please, tell no one. We do not want to deal with that godforsaken quest ever again.'),
('olmic_rune', 'The easiest quest for us to implement was The Olmic Rune, simply because it has no spoken dialogue.'),
('pain_in_updates', 'Every time a new Wynncraft update featuring quest updates is announced, a tiny part of all our staff members dies.'),
('post_fruma_usage', 'The daily user count of our mod trippled after the Fruma trailer was published.'),
('prick', 'Somewhere in the development of the mod, the Talking Mushroom''s voice got updated with re-recorded lines, but one line remained in its original state - the "PRICK" line! Not much you can change there...'),
('replacement_of_ai_voices', 'Voices of Wynn used to contain around 15 NPCs whose voices were generated by text-to-speech tools rather than real people. These were mostly robot NPCs with 1 to 3 lines, but we decided to take a stance against AI in creative projects and replaced them with real voices, which was done in just a few days. At this time, there are just 3 NPCs left without original voicing, but they''re somewhat special, you can check them out here: https://voicesofwynn.com/cast/498'),
('seaskipper_casting', 'After Biscuit, our most prolific voice actor, won the role of Seaskipper, kmaxi banned him from ever applying again.'),
('sewers_of_ragni', 'The Sewers of Ragni was at one point the only quest with multiple NPCs, all of which were voiced by the same voice actor. We later recast it.'),
('staff_team', 'Our staff team consists of 4 main roles: Log Collectors, Writers, Voice Managers and Sound Editors. If you feel like you could do any of these well, just message @kmaxi on Discord and ask to join.'),
('stop_ai_banner', 'Most of our community seems to hate AI. At one point, we organised an art competition to replace our Discord banner, which was previously AI generated.'),
('talking_mushroom_inspiration', 'There were two main inspirations for the Talking Mushroom voice - Gilbert Gottfried, and the English voice actor for "Dimple" from the anime "Mob Psycho 100".'),
('talking_mushroom_voice', 'In case you were worried, no, the Talking Mushroom voice does NOT hurt the voice actor''s throat! It''s perfectly within his throat''s capabilities! Doing the voice for too long causes issues though, so he takes it in small bites!'),
('trouble_sleeping', 'One of our voice actors said on our Discord, that they have trouble sleeping recently because they keep having dreams about voice acting for this mod.'),
('we_forgot_sorry', 'There are 25 NPCs (now maybe more) whose voice actors we forgot to note and now we don''t know who voiced them. You can check their list on https://voicesofwynn.com/cast/204'),
('website_php', 'Our website''s backend is written by Shady in PHP. Everyone else in our developer team seems to hate that language (unjustly, signed: Shady).'),
('yansur_son', 'One of the applicants for the role of Yansur was son of the voice actress voicing Qira. If he got picked, there would be literaly recordings of mother screaming at her son in the mod.');

--rollback DELETE FROM fun_fact;
--rollback DELETE FROM mod_broadcast;
--rollback DELETE FROM mod_release;
