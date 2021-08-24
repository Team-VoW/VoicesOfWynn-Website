<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\ContentManager;
use VoicesOfWynn\Models\Db;

class Npc extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		switch ($_SERVER['REQUEST_METHOD']) {
			case 'GET':
				return $this->get($args);
			case 'POST':
				return $this->post($args);
			case 'DELETE':
				return $this->delete($args);
			default:
				return false;
		}
	}
	
	/**
	 * Processing method for GET requests to this controller (NPC info webpage was requested)
	 * @param array $args
	 * @return bool
	 */
	private function get(array $args): bool
	{
		$npcId = $args[0];
		
		self::$data['base_description'] = 'Tool for the administrators to manage NPCs and assign voice actors to them.';
		
		$cnm = new ContentManager();
		$npc = $cnm->getNpc($npcId);
		self::$data['npc_npc'] = $npc;
		self::$data['npc_voiceActor'] = $npc->getVoiceActor();
		self::$data['npc_quest_recordings'] = $cnm->getNpcRecordings($npcId);
		if (!isset(self::$data['npc_uploadErrors'])) {
			self::$data['npc_uploadErrors'] = array();
		}
		
		self::$views[] = 'npc';
		self::$cssFiles[] = 'npc';
		self::$jsFiles[] = 'npc';
		
		return true;
	}
	
	/**
	 * Processing method for POST requests to this controller (new recordings were uploaded or a voice actor was
	 * changed)
	 * @param array $args
	 * @return bool
	 */
	private function post(array $args): bool
	{
		$npcId = $args[0];
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
				$npcId,
				$questId,
				$line,
				$filename
			));
		}
		header('Location: '.$_SERVER['REQUEST_URI']);
		return $this->get($args);
	}
	
	/**
	 * Processing method for DELETE requests to this controller (a recording is supposed to be deleted)
	 * @param array $args NPC id as the first element, Recording ID as the second element
	 * @return bool
	 */
	private function delete($args): void
	{
		$npcId = $args[0];
		$recordingId = $args[1];
		
		if (empty($npcId) || empty($recordingId)) {
			header("HTTP/1.1 400 Bad request");
			exit();
		}
		
		$result = Db::fetchQuery('SELECT file FROM recording WHERE recording_id = ? AND npc_id = ?;',
			array($recordingId, $npcId));
		if (empty($result)) {
			header("HTTP/1.1 404 Not Found");
			exit();
		}
		
		//Delete record from database
		$filename = $result['file'];
		$result = Db::executeQuery('DELETE FROM recording WHERE recording_id = ? AND npc_id = ? LIMIT 1;',
			array($recordingId, $npcId));
		if ($result) {
			//Delete file
			unlink('dynamic/recordings/'.$filename);
		}
		exit($result);
	}
}

