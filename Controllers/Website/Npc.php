<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\User;
use VoicesOfWynn\Models\Website\Npc as NpcModel;

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
		
		self::$data['npc_quest_recordings'] = $cnm->getNpcRecordings($this->npc->getId());
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
      $overwriteFiles = isset($_POST['overwrite']) && $_POST['overwrite'] === 'on';
      self::$data['npc_uploadErrors'] = array();
      $recordingsCount = count($_FILES['recordings']['name']);
      for ($i = 0; $i < $recordingsCount; $i++) {
          $filename = $_FILES['recordings']['name'][$i];
          $tempName = $_FILES['recordings']['tmp_name'][$i];
          $type = $_FILES['recordings']['type'][$i];
          $error = $_FILES['recordings']['error'][$i];

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

          $line = explode('.', explode('-', $filename)[2])[0];

          //In case a file with this name already exists, append "_([number])" to it (before the extension)
          //Increase the number for as long as files with the name exist (a bit like in Windows)
          $fileReplaced = false;
          if (file_exists('dynamic/recordings/'.$filename)) {
              if ($overwriteFiles) {
                  unlink('dynamic/recordings/'.$filename);
                  $fileReplaced = true;
              }
              else {
                  $filename = str_replace('.ogg', '_(1).ogg', $filename);
                  for ($j = 2; file_exists('dynamic/recordings/'.$filename); $j++) {
                      $filename = preg_replace('/_\(\d*\)\.ogg$/', '_('.$j.').ogg', $filename);
                  }
              }
          }

          move_uploaded_file($tempName, 'dynamic/recordings/'.$filename);

          if (!$fileReplaced)
          {
              //Insert a new database record only if a new recording file was created on the server
              (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO recording (npc_id,quest_id,line,file) VALUES (?,?,?,?)', array(
                  $this->npc->getId(),
                  $questId,
                  $line,
                  $filename
              ));
          }
      }
      header('Location: '.$_SERVER['REQUEST_URI']);
      return $this->get($args);
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
			unlink('dynamic/recordings/'.$filename);
		}
		exit($result);
	}
}

