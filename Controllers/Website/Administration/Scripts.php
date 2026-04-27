<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Storage\Storage;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\Quest;

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
        $flash   = $_SESSION['scripts_flash'] ?? [];
        unset($_SESSION['scripts_flash']);

        self::$data['scripts_quests']       = $quests;
        self::$data['scripts_users']        = $cm->getUsersForDropdown();
        self::$data['scripts_current_page'] = $page;
        self::$data['scripts_total_pages']  = (int)ceil($total / $perPage);
        self::$data['scripts_message']      = $flash['message'] ?? null;
        self::$data['scripts_error']        = $flash['error'] ?? null;
        self::$views[] = 'scripts';
        return 200;
    }

    private function post(): int
    {
        $action = $_POST['action'] ?? '';
        $message = null;
        $error = null;

        switch ($action) {
            case 'set-writer':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $writerId = !empty($_POST['writer_id']) ? (int)$_POST['writer_id'] : null;
                $quest = new Quest(['id' => $questId]);
                if (!$quest->loadFromId()) {
                    $error = 'Quest not found.';
                    break;
                }
                $quest->setWriter($writerId);
                $message = 'Writer updated.';
                break;

            case 'upload-script':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $quest = new Quest(['id' => $questId]);
                if (!$quest->loadFromId()) {
                    $error = 'Quest not found.';
                    break;
                }
                if (empty($_FILES['script_file']['tmp_name']) || $_FILES['script_file']['error'] !== UPLOAD_ERR_OK) {
                    $error = 'No file uploaded or upload error.';
                    break;
                }
                $degeneratedName = $quest->getDegeneratedName();
                Storage::get()->upload($_FILES['script_file']['tmp_name'], 'scripts/' . $degeneratedName . '.txt', 'text/plain');
                $message = 'Script file uploaded for "' . $degeneratedName . '".';
                break;

            case 'save-quest':
                $questId = (int)($_POST['quest_id'] ?? 0);
                $quest = new Quest(['id' => $questId]);
                if (!$quest->loadFromId()) {
                    $error = 'Quest not found.';
                    break;
                }
                $writerId = !empty($_POST['writer_id']) ? (int)$_POST['writer_id'] : null;
                $quest->setWriter($writerId);
                $degeneratedName = $quest->getDegeneratedName();
                if (!empty($_FILES['script_file']['tmp_name']) && $_FILES['script_file']['error'] === UPLOAD_ERR_OK) {
                    Storage::get()->upload($_FILES['script_file']['tmp_name'], 'scripts/' . $degeneratedName . '.txt', 'text/plain');
                }
                foreach (($_POST['npc_editors'] ?? []) as $npcId => $editorId) {
                    $editorSaved = $quest->setNpcEditor((int)$npcId, !empty($editorId) ? (int)$editorId : null);
                    if (!$editorSaved) {
                        $error = 'NPC not found for this quest.';
                        break 2;
                    }
                }
                $message = 'Quest saved.';
                break;

            default:
                return 400;
        }

        $_SESSION['scripts_flash'] = [
            'message' => $message,
            'error' => $error,
        ];

        $page = max(1, (int)($_GET['page'] ?? 1));
        header('Location: /administration/scripts?page=' . $page);
        header('HTTP/1.1 303 See Other');
        return 303;
    }
}
