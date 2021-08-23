<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\ContentManager;

class Npcs extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
        
        $cnm = new ContentManager();
        self::$data['npcs_quests'] = $cnm->getQuests();
        
        self::$views[] = 'npcs';
        
        return true;
    }
}

