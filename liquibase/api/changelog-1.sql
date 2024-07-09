--liquibase formatted sql

-- changeset kmaxi:1
SET NAMES utf8;
SET time_zone = '+00:00';
SET foreign_key_checks = 0;
SET sql_mode = 'NO_AUTO_VALUE_ON_ZERO';

USE api;

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS access_codes (
  access_code_id int(11) NOT NULL AUTO_INCREMENT,
  code char(16) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  discord_id bigint(20) NOT NULL,
  active tinyint(4) NOT NULL DEFAULT 1,
  PRIMARY KEY (access_code_id)
) ENGINE=InnoDB AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS report (
  report_id int(11) NOT NULL AUTO_INCREMENT,
  time_submitted timestamp NOT NULL DEFAULT current_timestamp(),
  chat_message varchar(319) NOT NULL,
  npc_name varchar(127) NOT NULL,
  player varchar(64) NOT NULL,
  pos_x mediumint(9) NOT NULL,
  pos_y mediumint(9) NOT NULL,
  pos_z mediumint(9) NOT NULL,
  reported_times int(11) NOT NULL DEFAULT 1,
  status enum('unprocessed','forwarded','rejected','accepted','fixed') NOT NULL DEFAULT 'unprocessed',
  PRIMARY KEY (report_id)
) ENGINE=MyISAM AUTO_INCREMENT=10187 DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS daily (
  daily_id int(11) NOT NULL AUTO_INCREMENT,
  date date NOT NULL,
  bootups int(10) unsigned NOT NULL,
  PRIMARY KEY (daily_id)
) ENGINE=InnoDB AUTO_INCREMENT=673 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS ping (
  ping_id int(11) NOT NULL AUTO_INCREMENT,
  time timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  uuid varchar(64) NOT NULL COMMENT 'sha256 hash',
  ip varchar(64) NOT NULL COMMENT 'sha256 hash',
  PRIMARY KEY (ping_id)
) ENGINE=InnoDB AUTO_INCREMENT=653218 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS total (
  total_id int(11) NOT NULL AUTO_INCREMENT,
  uuid varchar(64) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  PRIMARY KEY (total_id),
  UNIQUE KEY uuid (uuid)
) ENGINE=InnoDB AUTO_INCREMENT=653218 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
