<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\Quest AS QuestModel;

class Quest extends WebpageController
{
    private QuestModel $quest;

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        $this->quest = new QuestModel(array('degenerated_name' => array_shift($args)));
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get($args);
            case 'POST':
            case 'PUT':
            case 'DELETE':
                return 405;
            default:
                return 405;
        }
    }

    /**
     * Processing method for GET requests to this controller (quest credits webpage was requested)
     * @param array $args
     * @return int|bool TRUE if everything needed about the quest is obtained, FALSE if the quest of the selected degenerated name doesn't exist
     */
    private function get(array $args): int
    {
        self::$data['base_title'] = 'Credits for '; //Will be completed below
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Contents,Content,Credits,List,Voting,'; //Will be completed below
        self::$data['base_description'] = 'You can view credits for a certain quest on this webpage and see more details about it.';

        if ($this->quest === false) {
            return 404; //Quest with this degenerated name doesn't exist in the database
        }
        $cnm = new ContentManager();
        if (!$this->quest->loadFromDegeneratedName()) {
            return 404;
        }

        $this->quest->getNpcs(true);   //Also load and save NPCs themselves into attribute

        self::$data['quest_quest'] = $this->quest;

        self::$data['base_title'] .= $this->quest->getName();
        self::$data['base_keywords'] .= $this->quest->getName();

        self::$data['quest_upvoted'] = $cnm->getVotes('+');
        self::$data['quest_downvoted'] = $cnm->getVotes('-');

        self::$views[] = 'quest';
        self::$cssFiles[] = 'quest';
        self::$cssFiles[] = 'npc';
        self::$cssFiles[] = 'voting';
        self::$cssFiles[] = 'audio-player';
        self::$jsFiles[] = 'voting';
        self::$jsFiles[] = 'audio-player';
        self::$jsFiles[] = 'quest';

        return true;
    }
}