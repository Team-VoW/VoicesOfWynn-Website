<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;

class Npcs extends WebpageController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        if (!empty($args)) {
            return 400;
        }

        self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';

        self::$jsFiles[] = 'npcs';
        self::$views[] = 'npcs';

        return true;
    }
}
