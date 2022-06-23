<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\AccountManager;
use VoicesOfWynn\Models\ContentManager;
use VoicesOfWynn\Models\Db;

class Npc extends WebpageController
{
	private int $npcId;
	private bool $disallowAdministration;
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		$this->npcId = array_shift($args);
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
		$npc = $cnm->getNpc($this->npcId);
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
		
		self::$data['npc_quest_recordings'] = $cnm->getNpcRecordings($this->npcId);
		if (!$this->disallowAdministration && !isset(self::$data['npc_uploadErrors'])) {
			self::$data['npc_uploadErrors'] = array();
		}
		
        self::$data['npc_upvoted'] = $cnm->getVotes('+');
        self::$data['npc_downvoted'] = $cnm->getVotes('-');

		self::$views[] = 'npc';
		self::$cssFiles[] = 'npc';
		self::$cssFiles[] = 'voting';
		self::$jsFiles[] = 'voting';
		if (!$this->disallowAdministration) {
			self::$jsFiles[] = 'npc'; //Administrative functions
		}
		
		return true;
	}
	
	/**
	 * Processing method for POST requests to this controller (new recordings were uploaded or a voice actor was
	 * changed)
	 * @param array $args
	 * @return int|bool
	 */
	private function post(array $args): int
	{
		if ($this->disallowAdministration) {
			return 403;
		}
		
		$questId = $_POST['questId'];
		self::$data['npc_uploadErrors'] = array();
		
		for ($i = 1; isset($_FILES['recording'.$i]); $i++) {
			$filename = $_FILES['recording'.$i]['name'];
			$tempName = $_FILES['recording'.$i]['tmp_name'];
			$type = $_FILES['recording'.$i]['type'];
			$error = $_FILES['recording'.$i]['error'];
			$line = $_POST['line'.$i];
			
			if ($error !== UPLOAD_ERR_OK) {
				header("HTTP/1.1 422 Unprocessable Entity");
				self::$data['npc_uploadErrors'][$questId] = 'An error occurred during the file uploading: error code '.
				                                            $error;
				return $this->get($args);
			}
			
			if ($type !== 'audio/ogg') {
				header("HTTP/1.1 415 Unsupported Media Type");
				self::$data['npc_uploadErrors'][$questId] = 'One or more of the uploaded files is not in the correct format, only OGG files are allowed.';
				return $this->get($args);
			}
			
			move_uploaded_file($tempName, 'dynamic/recordings/'.$filename);
			Db::executeQuery('INSERT INTO recording (npc_id,quest_id,line,file) VALUES (?,?,?,?)', array(
				$this->npcId,
				$questId,
				$line,
				$filename
			));
		}
		header('Location: '.$_SERVER['REQUEST_URI']);
		return $this->get($args);
	}
	
	/**
	 * Processing method for PUT requests to this controller (new voice actor was set for this NPC)
	 * @param array $args NPC id as the first element, User ID as the second element
	 * @return int|bool
	 */
	private function put(array $args): int
	{
		if ($this->disallowAdministration) {
			return 403;
		}
		
		$userId = $args[0];
		
		if (empty($this->npcId) || empty($userId)) {
			return 400;
		}
		
		//Update the database
		$result = Db::executeQuery('UPDATE npc SET voice_actor_id = ? WHERE npc_id = ? LIMIT 1;', array($userId, $this->npcId));
		exit($result);
	}
	
	/**
	 * Processing method for DELETE requests to this controller (a recording is supposed to be deleted)
	 * @param array $args NPC id as the first element, Recording ID as the second element
	 * @return int|bool
	 */
	private function delete(array $args): int
	{
		if ($this->disallowAdministration) {
			return 403;
		}
		
		$recordingId = $args[0];
		
		if (empty($this->npcId) || empty($recordingId)) {
			return 400;
		}
		
		$result = Db::fetchQuery('SELECT file FROM recording WHERE recording_id = ? AND npc_id = ?;',
			array($recordingId, $this->npcId));
		if (empty($result)) {
			return 404;
		}
		
		//Delete record from database
		$filename = $result['file'];
		$result = Db::executeQuery('DELETE FROM recording WHERE recording_id = ? AND npc_id = ? LIMIT 1;',
			array($recordingId, $this->npcId));
		if ($result) {
			//Delete file
			unlink('dynamic/recordings/'.$filename);
		}
		exit($result);
	}
}

