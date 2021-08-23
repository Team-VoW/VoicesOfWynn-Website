<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\ContentManager;

class Npc extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		$npcId = $args[0];
		
		self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
		
		$cnm = new ContentManager();
		$npc = $cnm->getNpc($npcId);
		self::$data['npc_npc'] = $npc;
		self::$data['npc_voiceActor'] = $npc->getVoiceActor();
		self::$data['npc_quest_recordings'] = $cnm->getNpcRecordings($npcId);
		
		self::$views[] = 'npc';
		
		return true;
	}
}

