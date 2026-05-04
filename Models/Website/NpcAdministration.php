<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class NpcAdministration
{
    public function searchNpcs(string $term, int $limit): array
    {
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery(
            'SELECT npc_id, name, archived FROM npc WHERE name LIKE ? ORDER BY name LIMIT ?;',
            ['%' . $term . '%', $limit],
            true
        );

        if ($result === false) {
            return [];
        }

        $npcs = [];
        foreach ($result as $npcData) {
            $npcs[] = new Npc($npcData);
        }
        return $npcs;
    }

    public function searchQuestsWithNpcs(string $term): array
    {
        $db = new Db('Website/DbInfo.ini');
        $like = '%' . $term . '%';

        $idsResult = $db->fetchQuery('
            SELECT DISTINCT q.quest_id
            FROM quest q
            LEFT JOIN npc_quest nq ON nq.quest_id = q.quest_id
            LEFT JOIN npc n ON n.npc_id = nq.npc_id
            WHERE q.name LIKE ? OR n.name LIKE ?
            LIMIT 50;
        ', [$like, $like], true);

        if ($idsResult === false) {
            return [];
        }

        $ids = array_column($idsResult, 'quest_id');
        if (empty($ids)) {
            return [];
        }

        $placeholders = implode(',', array_fill(0, count($ids), '?'));
        $result = $db->fetchQuery('
            SELECT quest.quest_id, quest.name AS "qname", npc.npc_id, npc.name AS "nname", npc.voice_actor_id, npc.archived, user.user_id, user.display_name, COUNT(DISTINCT recording.recording_id) AS "recordings_count"
            FROM quest
            LEFT JOIN npc_quest ON npc_quest.quest_id = quest.quest_id
            LEFT JOIN npc ON npc.npc_id = npc_quest.npc_id
            LEFT JOIN recording ON recording.npc_id = npc.npc_id AND recording.quest_id = quest.quest_id
            LEFT JOIN user ON npc.voice_actor_id = user.user_id
            WHERE quest.quest_id IN (' . $placeholders . ')
            GROUP BY quest.quest_id, npc.npc_id, npc_quest.sorting_order
            ORDER BY quest.quest_id, npc_quest.sorting_order;
        ', $ids, true);

        if ($result === false) {
            return [];
        }

        return $this->buildQuestObjects($result);
    }

    private function buildQuestObjects(array $result): array
    {
        $quests = [];
        $currentQuest = null;
        foreach ($result as $npc) {
            if ($currentQuest === null || $currentQuest->getId() !== $npc['quest_id']) {
                if ($currentQuest !== null) {
                    $quests[] = $currentQuest;
                }
                $currentQuest = new Quest($npc);
            }
            if ($npc['npc_id'] === null) {
                continue;
            }
            $npcObj = new Npc($npc);
            if ($npc['user_id'] !== null) {
                $voiceActor = new User();
                $voiceActor->setData($npc);
                $npcObj->setVoiceActor($voiceActor);
            }
            $currentQuest->addNpc($npcObj);
        }
        if ($currentQuest !== null) {
            $quests[] = $currentQuest;
        }
        return $quests;
    }
}
