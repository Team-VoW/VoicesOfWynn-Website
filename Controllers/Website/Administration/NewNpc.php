<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\Npc;

class NewNpc extends WebpageController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
	        default:
	        	return 405;
        }
    }

    public function get(array $args): int
    {
        self::$data['base_description'] = 'Tool for the administrators to create new NPCs.';

        self::$data['newnpc_npcId'] = 0;
        self::$data['newnpc_error'] = '';

        $accountManager = new AccountManager();
        self::$data['newnpc_users'] = $accountManager->getUsers();

        $contentManager = new ContentManager();
        self::$data['newnpc_quests'] = $contentManager->getQuestList();

        self::$views[] = 'new-npc';
        return true;
    }

    public function post(array $args): int
    {
        $result = $this->get(array());

        $name = trim($_POST['name'] ?? '');
        if (empty($name)) {
            self::$data['newnpc_error'] = 'NPC name cannot be empty.';
            return $result;
        }

        $voiceActorId = !empty($_POST['voice_actor_id']) ? (int)$_POST['voice_actor_id'] : null;
        $questIds = $_POST['quest_ids'] ?? [];

        if (!is_array($questIds) || empty($questIds)) {
            self::$data['newnpc_error'] = 'At least one quest must be selected.';
            return $result;
        }

        $questIds = array_map('intval', $questIds);

        try {
            $npcId = Npc::create($name, $voiceActorId, $questIds);
            self::$data['newnpc_npcId'] = $npcId;
        } catch (\InvalidArgumentException $e) {
            self::$data['newnpc_error'] = $e->getMessage();
        } catch (\PDOException $e) {
            self::$data['newnpc_error'] = 'An error occurred while creating the NPC.';
        }
        return $result;
    }
}
