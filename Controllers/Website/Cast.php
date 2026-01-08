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
		$voiceActorId = $args[0];
		$cnm = new ContentManager();
		$voiceActor = $cnm->getVoiceActor($voiceActorId);
		if ($voiceActor === false) {
            //Voice actor with this ID doesn't exist
            return 404;
        }

		self::$data['base_title'] = $voiceActor->getName();
		self::$data['base_description'] = 'Do you really like a specific voice actor\'s recordings and would you like to find out more about them? Feel free to check out their personal webpage.';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Cast,Actor,Bio,Bios,List';
		
		self::$data['cast_voice_actor'] = $voiceActor;
		self::$data['cast_quest_recordings'] = $cnm->getVoiceActorRecordings($voiceActorId);
		
        self::$data['cast_upvoted'] = $cnm->getVotes('+');
        self::$data['cast_downvoted'] = $cnm->getVotes('-');

        self::$cssFiles[] = 'cast';
        self::$cssFiles[] = 'article-css-reset';
        self::$cssFiles[] = 'audio-player';
		self::$jsFiles[] = 'voting';
		self::$jsFiles[] = 'audio-player';
		self::$views[] = 'cast';
		return 200;	}
}

