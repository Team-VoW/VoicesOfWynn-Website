<?php

namespace VoicesOfWynn\Models;

class ContentManager
{
	public function getQuests(): array
	{
		$query = '
		SELECT quest.quest_id, quest.name AS "qname", npc.npc_id, npc.name AS "nname", npc.voice_actor_id, user.user_id, user.display_name, user.picture
		FROM quest
		JOIN npc_quest ON npc_quest.quest_id = quest.quest_id
		JOIN npc ON npc.npc_id = npc_quest.npc_id
		LEFT JOIN user ON npc.voice_actor_id = user.user_id
		ORDER BY quest.quest_id, npc.npc_id;
		';
		$result = Db::fetchQuery($query, array(), true);
		
		$quests = array();
		$currentQuest = null;
		foreach ($result as $npc) {
			if ($currentQuest === null || $currentQuest->getId() !== $npc['quest_id']) {
				//New quest encountered
				if ($currentQuest !== null) {
					$quests[] = $currentQuest;
				}
				$currentQuest = new Quest($npc);
			}
			
			$npcObj = new Npc($npc);
			if ($npc['user_id'] !== null) {
				$voiceActor = new User();
				$voiceActor->setData($npc);
				$npcObj->setVoiceActor($voiceActor);
			}
			$currentQuest->addNpc($npcObj);
		}
		$quests[] = $currentQuest;
		
		return $quests;
	}
	
	public function getNpc($id)
	{
		$query = '
		SELECT npc.npc_id, npc.name, user.user_id, user.display_name, user.picture
		FROM npc
		LEFT JOIN user ON npc.voice_actor_id = user.user_id
		WHERE npc_id = ?;';
		$result = Db::fetchQuery($query, array($id));
		
		if ($result === false) {
			return false;
		}
		$npc = new Npc($result);
		if ($result['user_id'] !== null) {
			$voiceActor = new User();
			$voiceActor->setData($result);
			$npc->setVoiceActor($voiceActor);
		}
		return $npc;
	}

    /**
     * @param $id int ID of the voice actor
     * @return User|false The User object containing all the data, or FALSE, if the user with this ID doesn't exist in the database
     */
	public function getVoiceActor($id)
	{
		$query = 'SELECT * FROM user WHERE user_id = ?;';
		$result = Db::fetchQuery($query, array($id));
		if ($result === false) {
            //Voice actor with this ID doesn't exist
            return false;
        }

		$voiceActor = new User();
		$voiceActor->setData($result);
		return $voiceActor;
	}
	
	public function getContributors(): array
	{
		$query = '
		SELECT `user`.user_id, `user`.display_name, `user`.picture, `user`.lore,
		GROUP_CONCAT(discord_role.name ORDER BY weight DESC) AS `roles`,
		GROUP_CONCAT(discord_role.color ORDER BY weight DESC) AS `role_colors`, (
			SELECT SUM(weight)
			FROM discord_role
			JOIN user_discord_role USING(discord_role_id)
			WHERE user_discord_role.user_id = user.user_id
		) AS `roles_weight`
		FROM user
		JOIN user_discord_role USING(user_id)
		JOIN discord_role USING(discord_role_id)
		GROUP BY user_id
		ORDER BY `roles_weight` DESC;
		';
		$result = Db::fetchQuery($query, array(), true);
		
		$users = array();
		foreach ($result as $userData) {
			$roleNames = explode(',', $userData['roles']);
			$roleColors = explode(',', $userData['role_colors']);
			$roles = array();
			for ($i = 0; $i < count($roleNames); $i++) {
				$roles[] = new DiscordRole($roleNames[$i], $roleColors[$i]); //Weight is not needed for the view
			}
			
			$user = new User();
			$user->setData(array(
				'id' => $userData['user_id'],
				'name' => $userData['display_name'],
				'avatar' => $userData['picture'],
				'lore' => $userData['lore']
			));
			
			$user->setRoles($roles);
			$users[] = $user;
		}
		return $users;
	}
	
	public function getVoiceActorRecordings($id): array
	{
		$query = '
		SELECT recording.recording_id, recording.quest_id, recording.line, recording.file, recording.upvotes, recording.downvotes, (SELECT COUNT(*) FROM comment WHERE comment.recording_id = recording.recording_id) AS "comments", npc.npc_id AS `npc` , npc.name AS `nname`, quest.name as `qname`
		FROM recording
		JOIN quest USING(quest_id)
		JOIN npc USING(npc_id)
		WHERE npc.voice_actor_id = ?
		ORDER BY quest_id, line;';
		$result = Db::fetchQuery($query, array($id), true);
		
		if ($result === false) {
			return array();
		}
		
		$currentQuest = null;
		$currentNpc = null;
		foreach ($result as $recording) {
			if ($currentQuest === null || $currentQuest->getId() !== $recording['quest_id']) {
				//New quest encountered
				if ($currentQuest !== null) {
					$currentQuest->addNpc($currentNpc);
					$quests[] = $currentQuest;
				}
				$currentQuest = new Quest($recording);
				$currentNpc = new Npc(array('id' => $recording['npc'], 'name' => $recording['nname'])); //"npc" is a key for NPC's ID
			}
			
			$recordingObj = new Recording($recording);
			$currentNpc->addRecording($recordingObj);
		}
		$currentQuest->addNpc($currentNpc);
		$quests[] = $currentQuest;
		return $quests;
	}
	
	public function getNpcRecordings($id): array
	{
		$query = '
		SELECT recording.recording_id, recording.quest_id, recording.line, recording.file, recording.upvotes, recording.downvotes, (SELECT COUNT(*) FROM comment WHERE comment.recording_id = recording.recording_id) AS "comments", quest.name
		FROM recording
		JOIN quest ON quest.quest_id = recording.quest_id
		WHERE npc_id = ?
		ORDER BY quest_id, line;';
		$result = Db::fetchQuery($query, array($id), true);
		
		$quests = array();
		if (gettype($result) !== 'array') {
			//No recordings yet
			$query = '
			SELECT quest_id,name
			FROM quest
		    JOIN npc_quest USING(quest_id)
			WHERE npc_id = ?;';
			$result = Db::fetchQuery($query, array($id), true);
			
			foreach ($result as $quest) {
				$currentQuest = new Quest($quest);
				$currentQuest->addNpc(new Npc(array('id' => $id)));
				$quests[] = $currentQuest;
			}
			return $quests;
		}
		
		$currentQuest = null;
		$currentNpc = null;
		foreach ($result as $recording) {
			if ($currentQuest === null || $currentQuest->getId() !== $recording['quest_id']) {
				//New quest encountered
				if ($currentQuest !== null) {
					$currentQuest->addNpc($currentNpc);
					$quests[] = $currentQuest;
				}
				$currentQuest = new Quest($recording);
				$currentNpc = new Npc(array('id' => $id));
			}
			
			$recordingObj = new Recording($recording);
			$currentNpc->addRecording($recordingObj);
		}
		$currentQuest->addNpc($currentNpc);
		$quests[] = $currentQuest;
		return $quests;
	}

    /**
     * Gets data about a single recording for the comment section
     * @param $recordingId int ID of the recording
     * @return false|Recording The Recording object containing all the data, or FALSE, if the recording doesn't exist in the database
     */
	public function getRecording($recordingId)
	{
		$result = Db::fetchQuery('SELECT * FROM recording WHERE recording_id = ?', array($recordingId));
        if ($result === false) {
            return false;
        }
        return new Recording(array(
			'id' => $recordingId,
			'npc_id' => $result['npc_id'],
			'quest_id' => $result['quest_id'],
			'line' => $result['line'],
			'file' => $result['file'],
			'upvotes' => $result['upvotes'],
			'downvotes' => $result['downvotes']
		));
	}
	
	public function getComments($recordingId): array
	{
		$result = Db::fetchQuery('
			SELECT comment_id,verified,user_id,ip,name,email,content,recording_id,
			CONCAT("https://www.gravatar.com/avatar/",MD5(email),"?d=identicon") AS gravatar
			FROM comment WHERE recording_id = ? ORDER BY comment_id DESC;
		', array($recordingId), true);
		if (empty($result)) {
			return array();
		}
		$comments = array();
		foreach ($result as $commentData) {
			$comments[] = new Comment($commentData);
		}
		return $comments;
	}
	
	/**
	 * Returns list of comments IDs on the current recording that were posted by the current user or current IP
	 * @param int $recordingId
	 * @return array
	 */
	public function getOwnedComments(int $recordingId): array
	{
		$userId = 0;    //No comment in the database should have 0 as value in the "user_id" column
		$ip = $_SERVER['REMOTE_ADDR'];
		if (isset($_SESSION['user'])) {
			$userId = $_SESSION['user']->getId();
		}
		
		$result = Db::fetchQuery('SELECT comment_id FROM comment WHERE ip = ? OR user_id = ?',
			array(inet_pton($ip), $userId), true);
		
		$ids = array();
		foreach ($result as $commentId) {
			$ids[] = $commentId['comment_id'];
		}
		return $ids;
	}
  
  /**
   * Returns list of recordings' IDs for which a vote of the specified type was casted from the current IP address
   * @param string $type Either "+" for upvotes or "-" for downvotes
   * @return array Array of recordings' IDs, or empty array if the current IP has no active votes
   * @throws \Exception
   */
  public function getVotes(string $type) {
    $result = Db::fetchQuery('SELECT recording_id FROM vote WHERE ip = ? AND type = ?;', array(inet_pton($_SERVER['REMOTE_ADDR']), $type), true);
    if (empty($result)) {
      return array();
    }
    $votes = array();
    foreach ($result as $row) {
      $votes[] = $row['recording_id'];
    }
    return $votes;
  }
  
	public function getRecordingTitle(Recording $recording): string
	{
		if (empty($recording->npc_id)) {
			$npcName = Db::fetchQuery('SELECT name FROM npc WHERE npc_id = (SELECT npc_id FROM recording WHERE recording_id = ?);',
				array($recording->id))['name'];
		} else {
			$npcName = Db::fetchQuery('SELECT name FROM npc WHERE npc_id = ?', array($recording->npc_id))['name'];
		}
		
		if (empty($recording->quest_id)) {
			$questName = Db::fetchQuery('SELECT name FROM quest WHERE quest_id = (SELECT quest_id FROM recording WHERE recording_id = ?);',
				array($recording->id))['name'];
		} else {
			$questName = Db::fetchQuery('SELECT name FROM quest WHERE quest_id = ?',
				array($recording->quest_id))['name'];
		}
		
		if (empty($recording->line)) {
			$lineNumber = Db::fetchQuery('SELECT line FROM recording WHERE recording_id = ?;',
				array($recording->id))['line'];
		} else {
			$lineNumber = $recording->line;
		}
		
		//<NPC name> in <quest name>, line <line number>
		return $npcName.' in '.$questName.', line n. '.$lineNumber;
	}
}

