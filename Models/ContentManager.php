<?php

namespace VoicesOfWynn\Models;

class ContentManager
{
	public function getQuests(): array
	{
		$query = '
		SELECT quest.quest_id, quest.name AS "qname", npc.npc_id, npc.name AS "nname", npc.voice_actor_id, user.user_id, user.display_name, user.picture
		FROM quest
		JOIN npc_quest ON npc_quest.quest_id = quest.quest_id
		JOIN npc ON npc.npc_id = npc_quest.npc_id
		LEFT JOIN user ON npc.voice_actor_id = user.user_id;
		';
		$result = Db::fetchQuery($query, array(), true);
		
		$quests = array();
		$currentQuest = null;
		foreach ($result as $npc) {
			if ($currentQuest === null || $currentQuest->getId() !== $npc['quest_id']) {
				//New quest encountered
				if ($currentQuest !== null) {
					$quests[] = $currentQuest;
				}
				$currentQuest = new Quest($npc);
			}
			
			$npcObj = new Npc($npc);
			if ($npc['user_id'] !== null) {
				$voiceActor = new User();
				$voiceActor->setData($npc);
				$npcObj->setVoiceActor($voiceActor);
			}
			$currentQuest->addNpc($npcObj);
		}
		$quests[] = $currentQuest;
		
		return $quests;
	}
	
	public function getNpc($id): Npc
	{
		$query = '
		SELECT npc.npc_id, npc.name, user.user_id, user.display_name, user.picture
		FROM npc
		LEFT JOIN user ON npc.voice_actor_id = user.user_id
		WHERE npc_id = ?;';
		$result = Db::fetchQuery($query, array($id));
		
		$npc = new Npc($result);
		if ($result['user_id'] !== null) {
			$voiceActor = new User();
			$voiceActor->setData($result);
			$npc->setVoiceActor($voiceActor);
		}
		return $npc;
	}
	
	public function getNpcRecordings($id): array
	{
		$query = '
		SELECT recording.recording_id, recording.quest_id, recording.line, recording.file, recording.upvotes, recording.downvotes, (SELECT COUNT(*) FROM comment WHERE comment.recording_id = recording.recording_id) AS "comments", quest.name
		FROM recording
		JOIN quest ON quest.quest_id = recording.quest_id
		WHERE npc_id = ?
		ORDER BY quest_id, line;';
		$result = Db::fetchQuery($query, array($id), true);
		
		$currentQuest = null;
		$currentNpc = null;
		foreach ($result as $recording) {
			if ($currentQuest === null || $currentQuest->getId() !== $recording['quest_id']) {
				//New quest encountered
				if ($currentQuest !== null) {
					$currentQuest->addNpc($currentNpc);
					$quests[] = $currentQuest;
				}
				$currentQuest = new Quest($recording);
				$currentNpc = new Npc(array('id' => $id));
			}
			
			$recordingObj = new Recording($recording);
			$currentNpc->addRecording($recordingObj);
		}
		$currentQuest->addNpc($currentNpc);
		$quests[] = $currentQuest;
		
		return $quests;
	}
}

