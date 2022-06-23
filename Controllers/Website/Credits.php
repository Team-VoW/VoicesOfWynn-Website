<?php


namespace VoicesOfWynn\Controllers\Website;


use VoicesOfWynn\Controllers\Controller;
use VoicesOfWynn\Models\ContentManager;

class Credits extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        self::$data['base_title'] = 'Credits';
        self::$data['base_description'] = 'Many people worked on this mod, especially many voice actors, both amateur and professional, were needed to complete it. Let\'s appreciate their efforts by looking at their contributions.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Credits';
    
		$cnm = new ContentManager();
		self::$data['credits_contributors'] = $cnm->getContributors();
		
		self::$cssFiles[] = 'credits';
	    self::$jsFiles[] = 'credits';
	    self::$jsFiles[] = 'font_rescale';
        self::$views[] = 'credits';
        return true;
    }
}