<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Storage\Storage;
use VoicesOfWynn\Models\Website\ContentManager;

class Scripts extends WebpageController
{
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get();
            case 'POST':
                return $this->post();
            default:
                return 405;
        }
    }

    private function get(): int
    {
        $page    = max(1, (int)($_GET['page'] ?? 1));
        $perPage = 25;
        $cm      = new ContentManager();
        $total   = $cm->countQuests();
        $quests  = $cm->getQuestsWithCredits($page, $perPage);

        $storage = Storage::get();
        foreach ($quests as &$quest) {
            $quest['has_script'] = $storage->exists('scripts/' . $quest['degenerated_name'] . '.txt');
            $quest['script_url'] = $storage->getUrl('scripts/' . $quest['degenerated_name'] . '.txt');
        }
        unset($quest);

        self::$data['scripts_quests']       = $quests;
        self::$data['scripts_users']        = $cm->getUsersForDropdown();
        self::$data['scripts_current_page'] = $page;
        self::$data['scripts_total_pages']  = (int)ceil($total / $perPage);
        self::$data['scripts_message']      = self::$data['scripts_message'] ?? null;
        self::$data['scripts_error']        = self::$data['scripts_error'] ?? null;
        self::$views[] = 'scripts';
        return 200;
    }

    private function post(): int
    {
        $action = $_POST['action'] ?? '';
        $cm = new ContentManager();

        switch ($action) {
            case 'set-writer':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $writerId = !empty($_POST['writer_id']) ? (int)$_POST['writer_id'] : null;
                if ($questId <= 0) {
                    self::$data['scripts_error'] = 'Invalid quest ID.';
                    break;
                }
                $cm->setQuestWriter($questId, $writerId);
                self::$data['scripts_message'] = 'Writer updated.';
                break;

            case 'upload-script':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $degeneratedName = $_POST['degenerated_name'] ?? '';
                if ($questId <= 0 || empty($degeneratedName)) {
                    self::$data['scripts_error'] = 'Invalid quest.';
                    break;
                }
                if (empty($_FILES['script_file']['tmp_name']) || $_FILES['script_file']['error'] !== UPLOAD_ERR_OK) {
                    self::$data['scripts_error'] = 'No file uploaded or upload error.';
                    break;
                }
                Storage::get()->upload($_FILES['script_file']['tmp_name'], 'scripts/' . $degeneratedName . '.txt', 'text/plain');
                self::$data['scripts_message'] = 'Script file uploaded for "' . htmlspecialchars($degeneratedName) . '".';
                break;

            case 'set-editor':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $npcId = (int)($_POST['npc_id'] ?? 0);
                $editorId = !empty($_POST['editor_id']) ? (int)$_POST['editor_id'] : null;
                if ($questId <= 0 || $npcId <= 0) {
                    self::$data['scripts_error'] = 'Invalid quest or NPC ID.';
                    break;
                }
                $cm->setNpcEditor($questId, $npcId, $editorId);
                self::$data['scripts_message'] = 'Editor updated.';
                break;

            case 'save-quest':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $degeneratedName = $_POST['degenerated_name'] ?? '';
                if ($questId <= 0) {
                    self::$data['scripts_error'] = 'Invalid quest ID.';
                    break;
                }
                $writerId = !empty($_POST['writer_id']) ? (int)$_POST['writer_id'] : null;
                $cm->setQuestWriter($questId, $writerId);
                if (!empty($_FILES['script_file']['tmp_name']) && $_FILES['script_file']['error'] === UPLOAD_ERR_OK) {
                    Storage::get()->upload($_FILES['script_file']['tmp_name'], 'scripts/' . $degeneratedName . '.txt', 'text/plain');
                }
                foreach (($_POST['npc_editors'] ?? []) as $npcId => $editorId) {
                    $cm->setNpcEditor($questId, (int)$npcId, !empty($editorId) ? (int)$editorId : null);
                }
                self::$data['scripts_message'] = 'Quest saved.';
                break;

            default:
                return 400;
        }

        return $this->get();
    }
}
