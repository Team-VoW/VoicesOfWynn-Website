<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\ContentManager;

class Cast extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		$voiceActorId = $args[0];
		$cnm = new ContentManager();
		$voiceActor = $cnm->getVoiceActor($voiceActorId);
		if ($voiceActor === false) {
            //Voice actor with this ID doesn't exist
            return false;
        }

		self::$data['base_title'] = $voiceActor->getName();
		self::$data['base_description'] = 'Do you really like a specific voice actor\'s recordings and would you like to find out more about them? Feel free to check out their personal webpage.';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Cast,Actor,Bio,Bios,List';
		
		self::$data['cast_voice_actor'] = $voiceActor;
		self::$data['cast_quest_recordings'] = $cnm->getVoiceActorRecordings($voiceActorId);
		
		self::$cssFiles[] = 'voting';
		self::$jsFiles[] = 'voting';
		self::$views[] = 'cast';
		return true;
	}
}