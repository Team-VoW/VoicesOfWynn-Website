--liquibase formatted sql

-- changeset kmaxi:1
SET NAMES utf8;
SET time_zone = '+00:00';
SET foreign_key_checks = 0;
SET sql_mode = 'NO_AUTO_VALUE_ON_ZERO';

USE website;

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS comment (
  comment_id int(11) NOT NULL AUTO_INCREMENT,
  verified tinyint(1) NOT NULL DEFAULT 0,
  user_id int(11) DEFAULT NULL,
  ip varbinary(16) DEFAULT NULL,
  name varchar(31) DEFAULT NULL,
  email varchar(255) DEFAULT NULL,
  content text NOT NULL,
  recording_id int(11) DEFAULT NULL,
  PRIMARY KEY (comment_id),
  KEY recording (recording_id),
  KEY user_id (user_id),
  CONSTRAINT comment_ibfk_1 FOREIGN KEY (recording_id) REFERENCES recording (recording_id) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT comment_ibfk_2 FOREIGN KEY (user_id) REFERENCES user (user_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=86 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS discord_role (
  discord_role_id int(11) NOT NULL AUTO_INCREMENT,
  name varchar(31) NOT NULL,
  color char(6) NOT NULL,
  weight smallint(6) NOT NULL COMMENT 'Used to determine order of users on the Credits page - sum of weights of all roles, descending',
  PRIMARY KEY (discord_role_id)
) ENGINE=InnoDB AUTO_INCREMENT=22 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS download (
  download_id int(11) NOT NULL AUTO_INCREMENT,
  release_type enum('alpha','beta','pre-release','release','patch') NOT NULL,
  mc_version varchar(8) NOT NULL DEFAULT '1.12.2',
  wynn_version varchar(8) NOT NULL,
  version varchar(8) NOT NULL,
  changelog text NOT NULL,
  release_date date NOT NULL DEFAULT current_timestamp(),
  filename varchar(31) NOT NULL COMMENT 'This is just the file name, under which the file is saved on the server. Not the name that the file will have after the download.',
  size int(11) unsigned NOT NULL COMMENT 'In bytes',
  downloaded_times int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (download_id)
) ENGINE=InnoDB AUTO_INCREMENT=52 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS npc (
  npc_id int(11) NOT NULL AUTO_INCREMENT,
  name varchar(63) NOT NULL,
  degenerated_name varchar(63) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  voice_actor_id int(11) DEFAULT NULL,
  archived tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (npc_id),
  KEY voice_actor_id (voice_actor_id),
  CONSTRAINT npc_ibfk_1 FOREIGN KEY (voice_actor_id) REFERENCES user (user_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=888 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS npc_quest (
  npc_quest_id int(11) NOT NULL AUTO_INCREMENT,
  quest_id int(11) NOT NULL,
  npc_id int(11) NOT NULL,
  sorting_order tinyint(3) unsigned NOT NULL,
  PRIMARY KEY (npc_quest_id),
  KEY quest_id (quest_id),
  KEY npc_id (npc_id),
  CONSTRAINT npc_quest_ibfk_1 FOREIGN KEY (npc_id) REFERENCES npc (npc_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT npc_quest_ibfk_2 FOREIGN KEY (quest_id) REFERENCES quest (quest_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=906 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS quest (
  quest_id int(11) NOT NULL AUTO_INCREMENT,
  name varchar(63) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,
  degenerated_name varchar(63) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  PRIMARY KEY (quest_id),
  UNIQUE KEY degeneratedname (degenerated_name)
) ENGINE=InnoDB AUTO_INCREMENT=186 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS recording (
  recording_id int(11) NOT NULL AUTO_INCREMENT,
  npc_id int(11) DEFAULT NULL,
  quest_id int(11) NOT NULL,
  line smallint(6) NOT NULL,
  file varchar(63) NOT NULL COMMENT 'Name of the file stored in the recordings folder containing the recording',
  upvotes int(11) NOT NULL DEFAULT 0,
  downvotes int(11) NOT NULL DEFAULT 0,
  archived tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (recording_id),
  KEY npc_id (npc_id),
  KEY quest_id (quest_id),
  CONSTRAINT recording_ibfk_1 FOREIGN KEY (npc_id) REFERENCES npc (npc_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT recording_ibfk_2 FOREIGN KEY (quest_id) REFERENCES quest (quest_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=13722 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS user (
  user_id int(11) NOT NULL AUTO_INCREMENT,
  discord_id bigint(20) DEFAULT NULL,
  display_name varchar(31) NOT NULL,
  email varchar(255) DEFAULT NULL,
  password varchar(255) NOT NULL,
  system_admin tinyint(1) NOT NULL DEFAULT 0,
  picture varchar(17) NOT NULL DEFAULT 'default.png' COMMENT 'Name of the file located in the avatars folder containing the profile picture of the user.',
  bio text DEFAULT NULL,
  lore varchar(63) DEFAULT NULL,
  public_email tinyint(4) NOT NULL DEFAULT 1 COMMENT 'Should the e-mail be publicly visible?',
  discord varchar(37) DEFAULT NULL COMMENT 'Discord name limit = 32 characters (+ 5 legacy support for #xxxx)',
  youtube varchar(56) DEFAULT NULL COMMENT 'The link to a channel starting with "https://www.youtube.com/c"',
  twitter varchar(15) DEFAULT NULL COMMENT 'The maximum length might be outdated',
  castingcallclub varchar(64) DEFAULT NULL COMMENT 'I don''t know the exact max length, I hope this will be sufficient',
  force_password_change tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (user_id)
) ENGINE=InnoDB AUTO_INCREMENT=459 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS user_discord_role (
  user_discord_role_id int(11) NOT NULL AUTO_INCREMENT,
  user_id int(11) NOT NULL,
  discord_role_id int(11) NOT NULL,
  PRIMARY KEY (user_discord_role_id),
  KEY user_id (user_id),
  KEY role_info (discord_role_id),
  CONSTRAINT user_discord_role_ibfk_1 FOREIGN KEY (discord_role_id) REFERENCES discord_role (discord_role_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT user_discord_role_ibfk_2 FOREIGN KEY (user_id) REFERENCES user (user_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=683 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS vote (
  vote_id int(11) NOT NULL AUTO_INCREMENT,
  recording_id int(11) DEFAULT NULL,
  ip varbinary(16) NOT NULL,
  type enum('+','-') NOT NULL,
  PRIMARY KEY (vote_id),
  KEY recording_id (recording_id),
  CONSTRAINT vote_ibfk_1 FOREIGN KEY (recording_id) REFERENCES recording (recording_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=3595 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

-- changeset kmaxi:2
ALTER TABLE download 
ADD COLUMN download_link varchar(255) DEFAULT NULL, 
MODIFY COLUMN filename varchar(31) DEFAULT NULL COMMENT 'This is just the file name, under which the file is saved on the server. Not the name that the file will have after the download.';