--liquibase formatted sql

-- changeset kmaxi:trial-voice-manager-role
-- Website role 23 corresponds to Discord role 835558288489578536 (Trial Voice Manager).
INSERT INTO discord_role (discord_role_id, name, color, weight)
VALUES (23, 'Trial Voice Manager', 'FFFFFF', 0);
--rollback DELETE FROM discord_role WHERE discord_role_id = 23 AND name = 'Trial Voice Manager';
