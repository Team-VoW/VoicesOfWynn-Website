<?php

namespace VoicesOfWynn\Controllers\Api\Administration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Website\Npc;
use VoicesOfWynn\Models\Website\NpcAdministration;
use VoicesOfWynn\Models\Website\NpcRearranger;
use VoicesOfWynn\Models\Website\Quest;

class Npcs extends ApiController
{
    private const ALLOWED_METHODS = [
        'swap' => 'PUT',
        'add-to-quest' => 'PUT',
        'remove-from-quest' => 'PUT',
        'delete-quest' => 'DELETE',
        'rename-quest' => 'PUT',
        'search' => 'GET',
        'list' => 'GET',
    ];

    /**
     * Handles admin NPC and quest management API actions.
     */
    public function process(array $args): int
    {
        if (!isset($_SESSION['user']) || !$_SESSION['user']->isSysAdmin()) {
            return 403;
        }

        $action = $args[0] ?? '';
        if (!isset(self::ALLOWED_METHODS[$action])) {
            return 400;
        }
        if ($_SERVER['REQUEST_METHOD'] !== self::ALLOWED_METHODS[$action]) {
            return 405;
        }

        switch ($action) {
            case 'swap':
                return $this->swap($args);
            case 'add-to-quest':
                return $this->addToQuest($args);
            case 'remove-from-quest':
                return $this->removeFromQuest($args);
            case 'delete-quest':
                return $this->deleteQuest($args);
            case 'rename-quest':
                return $this->renameQuest($args);
            case 'search':
                return $this->search();
            case 'list':
                return $this->listNpcs();
        }

        return 400;
    }

    private function swap(array $args): int
    {
        $questId = $args[1] ?? null;
        $npc1Id = $args[2] ?? null;
        $npc2Id = $args[3] ?? null;
        if ($questId === null || $npc1Id === null || $npc2Id === null) {
            return 400;
        }

        $npcRearranger = new NpcRearranger();
        return $npcRearranger->swap($questId, $npc1Id, $npc2Id) ? 204 : 500;
    }

    private function addToQuest(array $args): int
    {
        $questId = $args[1] ?? null;
        $npcId = $args[2] ?? null;
        if ($questId === null || $npcId === null) {
            return 400;
        }

        $quest = new Quest(['id' => $questId]);
        $npc = new Npc(['id' => $npcId]);
        if (!$quest->loadFromId() || !$npc->loadFromId()) {
            return 404;
        }

        return $npc->addToQuest($questId) ? 204 : 500;
    }

    private function removeFromQuest(array $args): int
    {
        $questId = $args[1] ?? null;
        $npcId = $args[2] ?? null;
        if ($questId === null || $npcId === null) {
            return 400;
        }

        $quest = new Quest(['id' => $questId]);
        $npc = new Npc(['id' => $npcId]);
        if (!$quest->loadFromId() || !$npc->loadFromId()) {
            return 404;
        }
        if (!$npc->isLinkedToQuest($questId)) {
            return 404;
        }

        return $npc->removeFromQuest($questId) ? 204 : 409;
    }

    private function deleteQuest(array $args): int
    {
        $questId = $args[1] ?? null;
        if ($questId === null) {
            return 400;
        }

        $quest = new Quest(['id' => $questId]);
        if (!$quest->loadFromId()) {
            return 404;
        }

        return $quest->delete() ? 204 : 409;
    }

    private function renameQuest(array $args): int
    {
        $questId = $args[1] ?? null;
        if ($questId === null) {
            return 400;
        }

        $body = json_decode(file_get_contents('php://input'), true);
        $name = trim($body['name'] ?? '');
        if ($name === '' || mb_strlen($name) > 63) {
            return 400;
        }

        $quest = new Quest(['id' => $questId]);
        if (!$quest->loadFromId()) {
            return 404;
        }

        return $quest->rename($name) ? 204 : 500;
    }

    private function search(): int
    {
        $q = trim($_GET['q'] ?? '');
        if (mb_strlen($q) > 63) {
            return 400;
        }
        if (mb_strlen($q) < 3) {
            echo json_encode([]);
            return 200;
        }

        $quests = (new NpcAdministration())->searchQuestsWithNpcs($q);
        echo json_encode(array_map(function (Quest $quest) {
            return $this->formatQuestRow($quest);
        }, $quests));
        return 200;
    }

    private function listNpcs(): int
    {
        $npcs = (new NpcAdministration())->getNpcs();
        echo json_encode(array_map(function (Npc $npc) {
            return [
                'id' => $npc->getId(),
                'name' => $npc->getName(),
                'archived' => $npc->isArchived(),
            ];
        }, $npcs));
        return 200;
    }

    private function formatQuestRow(Quest $quest): array
    {
        $questNpcs = $quest->getNpcs() ?? [];
        $questNpcIds = array_map(function (Npc $npc) {
            return $npc->getId();
        }, $questNpcs);
        $npcRows = array_map(function (Npc $npc) {
            $voiceActor = $npc->getVoiceActor();
            $canRemove = $npc->getRecordingsCount() === 0;
            return [
                'id' => $npc->getId(),
                'name' => $npc->getName(),
                'voice_actor_id' => $voiceActor?->getId(),
                'voice_actor_name' => $voiceActor?->getName(),
                'recordings_count' => $npc->getRecordingsCount(),
                'can_remove' => $canRemove,
                'remove_title' => $canRemove ? '' : 'NPCs with recordings linked to this quest cannot be removed.',
            ];
        }, $questNpcs);

        return [
            'quest_id' => $quest->getId(),
            'quest_name' => $quest->getName(),
            'npc_ids' => $questNpcIds,
            'npc_rows' => $npcRows,
        ];
    }
}
