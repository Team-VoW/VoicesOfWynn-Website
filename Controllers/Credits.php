<?php


namespace VoicesOfWynn\Controllers;


use VoicesOfWynn\Models\ContentManager;

class Credits extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        self::$data['base_title'] = 'Credits';
        self::$data['base_description'] = 'Many people worked on this mod, especially many voice actors, both amateur and professional, were needed to complete it. Let\'s appreciate their efforts by looking at their contributions.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Credits';
    
		$cnm = new ContentManager();
		self::$data['credits_contributors'] = $cnm->getContributors();
		
		self::$cssFiles[] = 'credits';
        self::$views[] = 'credits';
        return true;
    }
}