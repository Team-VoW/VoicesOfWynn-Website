<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\ContentManager;

class Npcs extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
        
        $cnm = new ContentManager();
        self::$data['npcs_quests'] = $cnm->getQuests();
        
        self::$views[] = 'npcs';
        
        return true;
    }
}

