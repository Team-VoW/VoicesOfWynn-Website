<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\NpcRearranger;

class Npcs extends WebpageController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        if (!empty($args)) {
            switch ($args[0]) {
                case 'swap':
                    $questId = $args[1];
                    $npc1Id = $args[2];
                    $npc2Id = $args[3];
                    $npcRearranger = new NpcRearranger();
                    $result = $npcRearranger->swap($questId, $npc1Id, $npc2Id);
                    return ($result) ? 204 : 500;
                /* More actions can be added here */
                default:
                    return 400;
            }
        }

        self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';

        $cnm = new ContentManager();
        self::$data['npcs_quests'] = $cnm->getQuests();

        self::$jsFiles[] = 'npcs';
        self::$views[] = 'npcs';

        return true;
    }
}

