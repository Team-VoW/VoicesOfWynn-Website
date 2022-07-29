<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\Recording;

class Comments extends WebpageController
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		switch ($_SERVER['REQUEST_METHOD']) {
			case 'GET':
				return $this->get($args);
			case 'DELETE':
				return $this->delete($args);
			default:
				return 405;
		}
	}
	
	/**
	 * Processing method for GET requests to this controller (list of comments was requested)
	 * @param array $args
	 * @return bool
	 */
	private function get($args)
	{
		self::$data['base_title'] = 'Comments';
		self::$data['base_description'] = 'Read all the comments posted on this recording or post a new one. Praise the voice actor for their performance or suggest what could be improved.';
		self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Contents,Content,Recording,Comments,Feedback';
		
		self::$data['comments_admin'] = (isset($_SESSION['user']) && $_SESSION['user']->isSysAdmin());
        self::$data['comments_logged_in'] = isset($_SESSION['user']);
        if (isset($_SESSION['user'])) {
	        self::$data['comments_user_id'] = $_SESSION['user']->getId();
	        self::$data['comments_user_name'] = $_SESSION['user']->getName();
	        self::$data['comments_user_avatar'] = 'dynamic/avatars/'.$_SESSION['user']->getAvatarLink();
        }
		
		$recordingId = array_shift($args);
		$cnm = new ContentManager();
		self::$data['comments_recording'] = $cnm->getRecording($recordingId);
        if (self::$data['comments_recording'] === false) {
            //Recording of the chosen ID was not found
            return 404;
        }
		self::$data['comments_voice_actor_id'] = $cnm->getNpc(self::$data['comments_recording']->npcId)->getVoiceActor()->getId();
		self::$data['comments_recording_title'] = $cnm->getRecordingTitle(self::$data['comments_recording']);
		self::$data['comments_comments'] = $cnm->getComments($recordingId);
		self::$data['comments_owned_comments'] = $cnm->getOwnedComments($recordingId);
		$color = ['red', 'yellow', 'green', 'blue', 'purple'][rand(0, 4)];
		$_SESSION['antispam'] = $color;
		self::$data['comments_antispam_color'] = $color;
		self::$data['comments_antispam_color_code'] = Recording::IDEAL_COLORS[$color];
		
		self::$cssFiles[] = 'comments';
		self::$jsFiles[] = 'comments';
		self::$jsFiles[] = 'md5';
		self::$views[] = 'comments';
		return true;
	}
	
	/**
	 * Processing method for DELETE requests to this controller (a comment is being deleted by a system admin or the author)
	 * Verification is done before affecting database
	 * @param array $args
	 * @return bool
	 */
	private function delete($args)
	{
        $db = new Db('Website/DbInfo.ini');

		$commentId = array_shift( $args);
		$commentData = $db->fetchQuery('SELECT user_id,ip FROM comment WHERE comment_id = ?', array($commentId));
		if ($commentData === false) {
			return 404; //No comment with this ID exists
		}
		$commentAuthorId = $commentData['user_id'];
		$commentAuthorIp = $commentData['ip'];
		
		if (
			(!isset($_SESSION['user']) || !$_SESSION['user']->isSysAdmin()) && //Admin not logged in
			(!isset($_SESSION['user']) || $_SESSION['user']->getId() !== $commentAuthorId) && //Comment author not logged in
			(inet_pton($_SERVER['REMOTE_ADDR']) !== $commentAuthorIp) //Client not accessing system from the same IP as from which the comment was posted
		) {
			//No user is logged in or the logged user is not system admin
			return 401;
		}
		
		if ($db->executeQuery('DELETE FROM comment WHERE comment_id = ?;', array($commentId))) {
			return 204;
		}
		return 500;
	}
}

