--liquibase formatted sql

-- changeset kmaxi:casting-voting
/*
  Casting voting moves from Discord reactions to the website. A round groups the characters of one
  casting (a CCC project, a Discord /opencasting, or a hand-built one) so old castings can be closed
  and archived instead of cluttering the voting page. Auditions carry the MP3 converted by the API
  into blob storage; source_ref keeps imports idempotent (CCC submission audio url or Discord thread id).
  Votes and comments are anonymous to other voters: only the casting managers' review reads user_id.
  A casting_character_done row with no votes for that character counts as an abstain.
*/
CREATE TABLE casting_round (
  round_id int(11) NOT NULL AUTO_INCREMENT,
  name varchar(100) NOT NULL,
  description varchar(2000) DEFAULT NULL,
  status enum('draft','open','closed','archived') NOT NULL DEFAULT 'draft',
  source enum('manual','ccc','discord') NOT NULL DEFAULT 'manual',
  source_ref varchar(255) DEFAULT NULL,
  voting_closes_at datetime DEFAULT NULL COMMENT 'UTC. Voting is closed once passed, even while status is still open',
  reveal_comments enum('after_done','always') NOT NULL DEFAULT 'after_done',
  import_status enum('idle','running','done','failed') NOT NULL DEFAULT 'idle',
  import_message varchar(500) DEFAULT NULL,
  created_by int(11) DEFAULT NULL,
  created_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (round_id),
  KEY casting_round_status (status),
  KEY casting_round_source (source, source_ref),
  CONSTRAINT casting_round_creator FOREIGN KEY (created_by) REFERENCES user (user_id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE casting_character (
  character_id int(11) NOT NULL AUTO_INCREMENT,
  round_id int(11) NOT NULL,
  name varchar(100) NOT NULL,
  quest_name varchar(100) DEFAULT NULL,
  direction varchar(2000) DEFAULT NULL,
  sort_order int(11) NOT NULL DEFAULT 0,
  winner_audition_id int(11) DEFAULT NULL,
  PRIMARY KEY (character_id),
  UNIQUE KEY casting_character_name (round_id, name),
  CONSTRAINT casting_character_round FOREIGN KEY (round_id) REFERENCES casting_round (round_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE casting_audition (
  audition_id int(11) NOT NULL AUTO_INCREMENT,
  character_id int(11) NOT NULL,
  `number` int(11) NOT NULL,
  auditionee_name varchar(100) NOT NULL,
  auditionee_user_id int(11) DEFAULT NULL,
  source_ref varchar(500) DEFAULT NULL,
  source_ref_hash binary(32) GENERATED ALWAYS AS (UNHEX(SHA2(source_ref, 256))) STORED,
  audio_blob_path varchar(255) NOT NULL,
  duration_seconds double DEFAULT NULL,
  created_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (audition_id),
  UNIQUE KEY casting_audition_number (character_id, `number`),
  UNIQUE KEY casting_audition_source (character_id, source_ref_hash),
  CONSTRAINT casting_audition_character FOREIGN KEY (character_id) REFERENCES casting_character (character_id) ON DELETE CASCADE,
  CONSTRAINT casting_audition_user FOREIGN KEY (auditionee_user_id) REFERENCES user (user_id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

ALTER TABLE casting_character
  ADD CONSTRAINT casting_character_winner FOREIGN KEY (winner_audition_id) REFERENCES casting_audition (audition_id) ON DELETE SET NULL;

CREATE TABLE casting_vote (
  audition_id int(11) NOT NULL,
  user_id int(11) NOT NULL,
  comment varchar(1000) DEFAULT NULL,
  created_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (audition_id, user_id),
  KEY casting_vote_user (user_id),
  CONSTRAINT casting_vote_audition FOREIGN KEY (audition_id) REFERENCES casting_audition (audition_id) ON DELETE CASCADE,
  CONSTRAINT casting_vote_user FOREIGN KEY (user_id) REFERENCES user (user_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE casting_character_done (
  character_id int(11) NOT NULL,
  user_id int(11) NOT NULL,
  done_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (character_id, user_id),
  KEY casting_done_user (user_id),
  CONSTRAINT casting_done_character FOREIGN KEY (character_id) REFERENCES casting_character (character_id) ON DELETE CASCADE,
  CONSTRAINT casting_done_user FOREIGN KEY (user_id) REFERENCES user (user_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
--rollback DROP TABLE casting_character_done;
--rollback DROP TABLE casting_vote;
--rollback ALTER TABLE casting_character DROP FOREIGN KEY casting_character_winner;
--rollback DROP TABLE casting_audition;
--rollback DROP TABLE casting_character;
--rollback DROP TABLE casting_round;
