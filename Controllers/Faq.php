<?php

namespace VoicesOfWynn\Controllers;

class Faq extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		self::$data['base_title'] = 'FAQ';
		self::$data['base_description'] = 'Do you have a question about the mod? Then you\'ll most likely find it here';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,FAQ,Question,Questions,Answer,Answers';
		
		self::$views[] = 'faq';
		return true;
	}
}