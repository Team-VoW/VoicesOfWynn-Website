<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\ContentManager;

class Contents extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		self::$data['base_title'] = 'Mod Contents';
		self::$data['base_description'] = 'Would you like to find out what voice does a certain NPC have in our mod, but you can\'t do the quest it appears in? You can play any recording from the mod here, rate them with votes and comment on them.';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Contents,Content,Recordings,List,Voting';
		
		$cnm = new ContentManager();
		self::$data['contents_quests'] = $cnm->getQuests();
		
		self::$cssFiles[] = 'contents';
		self::$jsFiles[] = 'search_bar';
		self::$views[] = 'contents';
		return true;
	}
}