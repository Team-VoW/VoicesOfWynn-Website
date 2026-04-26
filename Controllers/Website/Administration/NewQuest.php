<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\Quest;

class NewQuest extends WebpageController
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
        self::$data['base_description'] = 'Tool for the administrators to create new quests.';

        self::$data['newquest_questId'] = 0;
        self::$data['newquest_error'] = '';

        self::$views[] = 'new-quest';
        return true;
    }

    public function post(array $args): int
    {
        $result = $this->get(array());

        $name = trim($_POST['name'] ?? '');
        if (empty($name)) {
            self::$data['newquest_error'] = 'Quest name cannot be empty.';
            return $result;
        }

        try {
            $questId = Quest::create($name);
            self::$data['newquest_questId'] = $questId;
        } catch (\InvalidArgumentException $e) {
            self::$data['newquest_error'] = $e->getMessage();
            return $result;
        } catch (\PDOException $e) {
            if ($e->getCode() === '23000') {
                self::$data['newquest_error'] = 'A quest with this name (or a similar degenerated name) already exists.';
            } else {
                self::$data['newquest_error'] = 'An error occurred while creating the quest.';
            }
        }
        return $result;
    }
}
