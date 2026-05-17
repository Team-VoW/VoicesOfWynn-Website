--liquibase formatted sql

-- changeset shady:1

USE api;

SET NAMES utf8mb4;

ALTER TABLE report
  CHANGE npc_name npc_name VARCHAR(127) CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci NULL,
  CHANGE `pos_x` `pos_x` MEDIUMINT NULL,
  CHANGE `pos_y` `pos_y` MEDIUMINT NULL,
  CHANGE `pos_z` `pos_z` MEDIUMINT NULL;

ALTER TABLE `report` ADD UNIQUE(`chat_message`);