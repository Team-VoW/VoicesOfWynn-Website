<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\RecordingUploader;
use VoicesOfWynn\Models\Website\User;
use VoicesOfWynn\Models\Website\Npc as NpcModel;
use VoicesOfWynn\Models\Storage\Storage;

class Npc extends WebpageController
{
	private NpcModel $npc;
	private bool $disallowAdministration;
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
        $this->npc = new NpcModel(array('id' => array_shift($args)));
		$this->disallowAdministration = !((array_shift($args) === 'false'));
		switch ($_SERVER['REQUEST_METHOD']) {
			case 'GET':
				return $this->get($args);
			case 'POST':
				return $this->post($args);
			case 'PUT':
				return $this->put($args);
			case 'DELETE':
				return $this->delete($args);
			default:
				return 405;
		}
	}
	
	/**
	 * Processing method for GET requests to this controller (NPC info webpage was requested)
	 * @param array $args
	 * @return int|bool TRUE if everything needed about the NPC is obtained, FALSE if the NPC of the selected ID doesn't exist
	 */
	private function get(array $args): int
	{
		if ($this->disallowAdministration) {
			self::$data['base_title'] = 'Recordings for '; //Will be completed below
			self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Contents,Content,Recordings,List,Voting,'; //Will be completed below
			self::$data['base_description'] = 'You can listen to all recordings of a certain NPC on this webpage and see more details about it.';
		}
		else {
			self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
		}
		
		self::$data['npc_admin'] = !$this->disallowAdministration;
		
		$cnm = new ContentManager();
		$npc = $cnm->getNpc($this->npc->getId());
        if ($npc === false) {
            return 404; //NPC with this ID doesn't exist in the database
        }
		self::$data['npc_npc'] = $npc;
		self::$data['npc_voice_actor'] = $npc->getVoiceActor();
		
		if (!$this->disallowAdministration) {
			$acManager = new AccountManager();
			self::$data['npc_voice_actors'] = $acManager->getUsers();
		}
		else {
			self::$data['base_title'] .= $npc->getName();
			self::$data['base_keywords'] .= $npc->getName();
		}
		
		$questRecordings = $cnm->getNpcRecordings($this->npc->getId());
		$cardQuestNames = [];
		$cardRecordingsByQuest = [];
		$cardAllRecordings = [];
		foreach ($questRecordings as $quest) {
			$npcs = $quest->getNpcs();
			$recs = !empty($npcs) ? $npcs[0]->getRecordings() : [];
			$cardQuestNames[$quest->getId()] = $quest->getName();
			$cardRecordingsByQuest[$quest->getId()] = $recs;
			$cardAllRecordings = array_merge($cardAllRecordings, $recs);
		}
		self::$data['npc_card_quest_names']         = $cardQuestNames;
		self::$data['npc_card_recordings_by_quest'] = $cardRecordingsByQuest;
		self::$data['npc_card_all_recordings']      = $cardAllRecordings;
		if (!$this->disallowAdministration && !isset(self::$data['npc_uploadErrors'])) {
			self::$data['npc_uploadErrors'] = array();
		}

        $uuid = $this->loadUUID(); //Also saves UUID in $_SESSION
        self::$data['npc_uuid'] = $uuid;
        self::$data['npc_was_upvoted'] = $npc->wasVotedFor(hash('sha256', $uuid ?? $_SERVER['REMOTE_ADDR']), "+");
        self::$data['npc_was_downvoted'] = $npc->wasVotedFor(hash('sha256', $uuid ?? $_SERVER['REMOTE_ADDR']), "-");

		self::$views[] = 'npc';
		self::$cssFiles[] = 'npc-card';
		self::$cssFiles[] = 'npc';
		self::$cssFiles[] = 'voting';
		self::$cssFiles[] = 'audio-player';
		self::$cssFiles[] = 'comments';
		self::$cssFiles[] = 'comments-dialog';
		self::$jsFiles[] = 'voting';
		self::$jsFiles[] = 'audio-player';
		self::$jsFiles[] = 'cast-accordion';
		self::$jsFiles[] = 'npc'; //Scroll animations + admin functions (admin UI not rendered for non-admins, so handlers attach to nothing)
		self::$jsFiles[] = 'md5';
		self::$jsFiles[] = 'comments-dialog';

		return true;
	}
	
	/**
	 * Processing method for PUT requests to this controller (recast, archivation of NPC or archivation of recordings)
	 * @param array $args Voice actor id as the first element
	 * @return int|bool
	 */
	private function put(array $args): int
	{
        if ($this->disallowAdministration) {
            return 403;
        }

        if (empty($this->npc)) {
            return 400;
        }

        switch (array_shift($args)) {
            case 'recast':
                $user = new User();
                $user->setData(array('id' => array_shift($args)));
                if (empty($user)) {
                    return 400;
                }
                $result = $this->npc->recast($user);
                return ($result) ? 204 : 500;
            case 'archive':
                $result = $this->npc->archive();
                header('Content-Type: application/json');
                header('HTTP/1.1 303 See Other');
                echo json_encode(array('Location' => '/administration/npcs/manage/'.$result));
                return ($result) ? 303 : 500;
            case 'archive-quest-recordings':
                $questId = array_shift($args);
                if (empty($questId)) {
                    return 400;
                }
                $result = $this->npc->archiveQuestRecordings($questId);
                return ($result) ? 204 : 500;
            default:
                return 400;
        }
	}
	
	/**
	 * Processing method for DELETE requests to this controller (a recording is supposed to be deleted)
	 * @param array $args Recording ID as the first element
	 * @return int|bool
	 */
	private function delete(array $args): int
	{
		if ($this->disallowAdministration) {
			return 403;
		}
		
		$recordingId = $args[0];
		
		if (empty($this->npc->getId()) || empty($recordingId)) {
			return 400;
		}

        $db = new Db('Website/DbInfo.ini');
		$result = $db->fetchQuery('SELECT file FROM recording WHERE recording_id = ? AND npc_id = ?;',
			array($recordingId, $this->npc->getId()));
		if (empty($result)) {
			return 404;
		}
		
		//Delete record from database
		$filename = $result['file'];
		$result = $db->executeQuery('DELETE FROM recording WHERE recording_id = ? AND npc_id = ? LIMIT 1;',
			array($recordingId, $this->npc->getId()));
		if ($result) {
			//Delete file
			Storage::get()->delete('recordings/'.$filename);
		}
		exit($result);
	}
}

