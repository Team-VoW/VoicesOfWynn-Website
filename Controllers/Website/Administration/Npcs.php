<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\Npc as NpcModel;
use VoicesOfWynn\Models\Website\NpcRearranger;
use VoicesOfWynn\Models\Website\Quest;

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
                case 'add-to-quest':
                    if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
                        return 405;
                    }
                    $questId = $args[1] ?? null;
                    $npcId = $args[2] ?? null;
                    if ($questId === null || $npcId === null) {
                        return 400;
                    }
                    $quest = new Quest(array());
                    $npc = new NpcModel(array());
                    if (!$quest->loadFromId($questId) || !$npc->loadFromId($npcId)) {
                        return 404;
                    }
                    $result = $npc->addToQuest($questId);
                    return ($result) ? 204 : 500;
                case 'remove-from-quest':
                    if ($_SERVER['REQUEST_METHOD'] !== 'DELETE') {
                        return 405;
                    }
                    $questId = $args[1] ?? null;
                    $npcId = $args[2] ?? null;
                    if ($questId === null || $npcId === null) {
                        return 400;
                    }
                    $quest = new Quest(array());
                    $npc = new NpcModel(array());
                    if (!$quest->loadFromId($questId) || !$npc->loadFromId($npcId)) {
                        return 404;
                    }
                    if (!$npc->isLinkedToQuest($questId)) {
                        return 404;
                    }
                    if ($npc->hasRecordingsInQuest($questId)) {
                        return 409;
                    }
                    $result = $npc->removeFromQuest($questId);
                    return ($result) ? 204 : 500;
                case 'search':
                    $q = trim($_GET['q'] ?? '');
                    if (mb_strlen($q) > 63) {
                        http_response_code(400);
                        exit();
                    }
                    if ($q === '') {
                        header('Content-Type: application/json');
                        echo json_encode([]);
                        exit();
                    }
                    $quests = (new Quest(array()))->searchWithNpcs($q);
                    $rows = array_map(function (Quest $quest) {
                        $questNpcs = $quest->getNpcs() ?? array();
                        $questNpcIds = array_map(fn($npc) => $npc->getId(), $questNpcs);
                        $npcRows = array_map(function (NpcModel $npc) {
                            $voiceActor = $npc->getVoiceActor();
                            $canRemove = $npc->getRecordingsCount() === 0;
                            return array(
                                'id' => $npc->getId(),
                                'name' => $npc->getName(),
                                'voice_actor_id' => $voiceActor?->getId(),
                                'voice_actor_name' => $voiceActor?->getName(),
                                'recordings_count' => $npc->getRecordingsCount(),
                                'can_remove' => $canRemove,
                                'remove_title' => $canRemove ? '' : 'NPCs with recordings linked to this quest cannot be removed.',
                            );
                        }, $questNpcs);
                        return array(
                            'quest_id' => $quest->getId(),
                            'quest_name' => $quest->getName(),
                            'npc_ids' => $questNpcIds,
                            'npc_rows' => $npcRows,
                        );
                    }, $quests);
                    header('Content-Type: application/json');
                    echo json_encode($rows);
                    exit();
                case 'autocomplete':
                    $q = trim($_GET['q'] ?? '');
                    if (mb_strlen($q) > 63) {
                        http_response_code(400);
                        exit();
                    }
                    if ($q === '') {
                        header('Content-Type: application/json');
                        echo json_encode([]);
                        exit();
                    }
                    $npcs = (new NpcModel(array()))->search($q, 20);
                    $data = array_map(function (NpcModel $npc) {
                        return array(
                            'id' => $npc->getId(),
                            'name' => $npc->getName(),
                            'archived' => $npc->isArchived(),
                        );
                    }, $npcs);
                    header('Content-Type: application/json');
                    echo json_encode($data);
                    exit();
                default:
                    return 400;
            }
        }

        self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';

        self::$jsFiles[] = 'npcs';
        self::$views[] = 'npcs';

        return true;
    }
}
