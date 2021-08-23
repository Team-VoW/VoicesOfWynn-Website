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
}