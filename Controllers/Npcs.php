<?php

namespace VoicesOfWynn\Controllers;

class Npcs extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
        
        // TODO: Implement process() method.
        
        self::$views[] = 'npcs';
        
        return true;
    }
}

