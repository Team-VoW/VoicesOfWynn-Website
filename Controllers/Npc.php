<?php

namespace VoicesOfWynn\Controllers;

class Npc extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		$npcId = $args[0];
		
		self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
		
		self::$data['npc_id'] = $npcId;
		
		self::$views[] = 'npc';
		
		return true;
	}
}