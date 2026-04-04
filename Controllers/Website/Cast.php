<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Website\ContentManager;

class Cast extends WebpageController
{

	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		$contributorId = $args[0];
		$cnm = new ContentManager();
		$voiceActor = $cnm->getVoiceActor($contributorId);
		if ($voiceActor === false) {
			//Voice actor with this ID doesn't exist
			return 404;
		}

		self::$data['base_title'] = $voiceActor->getName();
		self::$data['base_description'] = 'Do you really like a specific voice actor\'s recordings and would you like to find out more about them? Feel free to check out their personal webpage.';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Cast,Actor,Bio,Bios,List';

		self::$data['cast_voice_actor'] = $voiceActor;

		$questRecordings = $cnm->getVoiceActorRecordings($contributorId);
		$npcGroups = [];
		foreach ($questRecordings as $quest) {
			foreach ($quest->getNpcs() as $npc) {
				$npcId = $npc->getId();
				if (!isset($npcGroups[$npcId])) {
					$npcGroups[$npcId] = [
						'npc' => $npc,
						'recordings' => [],
						'questNames' => [],
						'questDegeneratedNames' => [],
						'recordingsByQuest' => [],
					];
				}
				foreach ($npc->getRecordings() as $recording) {
					$npcGroups[$npcId]['recordings'][] = $recording;
					$npcGroups[$npcId]['recordingsByQuest'][$quest->getId()][] = $recording;
				}
				$npcGroups[$npcId]['questNames'][$quest->getId()] = $quest->getName();
				$npcGroups[$npcId]['questDegeneratedNames'][$quest->getId()] = $quest->getDegeneratedName();
			}
		}
		self::$data['cast_npc_groups'] = array_values($npcGroups);
		self::$data['cast_scripted_quests'] = $cnm->getWritersQuests($contributorId);
		self::$data['cast_edited_npcs'] = $cnm->getEditorsNpcsByQuests($contributorId);

        $uuid = $this->loadUUID(); //Also saves UUID in $_SESSION
        self::$data['cast_uuid'] = $uuid;
		self::$data['cast_upvoted'] = $cnm->getVotes(hash('sha256', $uuid ?? $_SERVER['REMOTE_ADDR']), '+', null, $voiceActor);
		self::$data['cast_downvoted'] = $cnm->getVotes(hash('sha256', $uuid ?? $_SERVER['REMOTE_ADDR']), '-', null, $voiceActor);

		self::$cssFiles[] = 'npc-card';
		self::$cssFiles[] = 'cast';
		self::$cssFiles[] = 'article-css-reset';
		self::$cssFiles[] = 'audio-player';
		self::$cssFiles[] = 'voting';
		self::$cssFiles[] = 'comments';
		self::$cssFiles[] = 'comments-dialog';
		self::$jsFiles[] = 'audio-player';
		self::$jsFiles[] = 'cast-accordion';
		self::$jsFiles[] = 'voting';
		self::$jsFiles[] = 'md5';
		self::$jsFiles[] = 'comments-dialog';
		self::$views[] = 'cast';
		return 200;
	}
}
