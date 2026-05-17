<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Controllers\Controller;

class Faq extends WebpageController
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		self::$data['base_title'] = 'FAQ';
		self::$data['base_description'] = 'Do you have a question about the mod? Then you\'ll most likely find it here';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,FAQ,Question,Questions,Answer,Answers';
		
		self::$cssFiles[] = 'faq';
		self::$cssFiles[] = 'bubble';
		self::$views[] = 'faq';
		return true;
	}
}