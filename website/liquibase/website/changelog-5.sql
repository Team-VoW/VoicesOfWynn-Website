--liquibase formatted sql

--changeset vow:quest-feedback
CREATE TABLE quest_feedback (
  submission_id char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
  installation_id char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  quest_id int(11) NULL,
  original_quest_name varchar(200) NOT NULL,
  grouping_key varchar(200) COLLATE utf8mb4_bin NOT NULL,
  score tinyint unsigned NOT NULL,
  comment varchar(2000) NULL,
  mod_version varchar(64) NOT NULL,
  created_at datetime(6) NOT NULL,
  updated_at datetime(6) NOT NULL,
  edit_token_hash binary(32) NOT NULL,
  CONSTRAINT quest_feedback_score CHECK (score BETWEEN 1 AND 5),
  CONSTRAINT quest_feedback_quest FOREIGN KEY (quest_id) REFERENCES quest(quest_id) ON DELETE SET NULL,
  INDEX feedback_group_date (grouping_key, created_at),
  INDEX feedback_date (created_at),
  INDEX feedback_installation (installation_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;

CREATE TABLE quest_feedback_write_limit (
  bucket_key binary(32) NOT NULL,
  window_start datetime NOT NULL,
  writes int unsigned NOT NULL,
  PRIMARY KEY (bucket_key, window_start)
) ENGINE=InnoDB;
--rollback DROP TABLE quest_feedback_write_limit;
--rollback DROP TABLE quest_feedback;
