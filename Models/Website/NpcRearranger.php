<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class NpcRearranger
{
    public function swap(int $questId, int $npc1Id, int $npc2Id): bool
    {
        $db = new Db('Website/DbInfo.ini');
        $db->startTransaction();

        //Brace yourself. You can use this image to combat the code below: https://cdn.discordapp.com/attachments/943819263116988446/1003306660418302032/swap.png

        $db->executeQuery('
            UPDATE npc_quest SET sorting_order = (
                (
                SELECT sorting_order FROM npc_quest WHERE npc_id = ? AND quest_id = ? LIMIT 1
                ) + sorting_order
            ) WHERE npc_id = ? AND quest_id = ? LIMIT 1;
        ', array($npc1Id, $questId, $npc2Id, $questId)); //Addition

        $db->executeQuery('
            UPDATE npc_quest SET sorting_order = (
                (
                SELECT sorting_order FROM npc_quest WHERE npc_id = ? AND quest_id = ? LIMIT 1
                ) - sorting_order
            ) WHERE npc_id = ? AND quest_id = ? LIMIT 1;
        ', array($npc2Id, $questId, $npc1Id, $questId)); //Subtraction 1

        $db->executeQuery('
            UPDATE npc_quest SET sorting_order = (
                sorting_order - (
                SELECT sorting_order FROM npc_quest WHERE npc_id = ? AND quest_id = ? LIMIT 1
                )
            ) WHERE npc_id = ? AND quest_id = ? LIMIT 1;
        ', array($npc1Id, $questId, $npc2Id, $questId)); //Subtraction 2

        return $db->commitTransaction();
    }
}

