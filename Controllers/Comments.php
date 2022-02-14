<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\ContentManager;
use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Recording;

class Comments extends Controller
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		switch ($_SERVER['REQUEST_METHOD']) {
			case 'GET':
				return $this->get($args);
			case 'DELETE':
				return $this->delete($args);
			default:
				return false;
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
            return false;
        }
		self::$data['comments_recording_title'] = $cnm->getRecordingTitle(self::$data['comments_recording']);
		self::$data['comments_comments'] = $cnm->getComments($recordingId);
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
	 * Processing method for DELETE requests to this controller (a comment is being deleted by a system admin)
	 * Verification is done before affecting database
	 * @param array $args
	 * @return bool
	 */
	private function delete($args)
	{
		if (!isset($_SESSION['user']) || !$_SESSION['user']->isSysAdmin()) {
			//No user is logged in or the logged user is not system admin
			header('HTTP/1.1 401 Unauthorized');
			exit();
		}
		
		$commentId = array_shift($args);
		if (Db::executeQuery('DELETE FROM comment WHERE comment_id = ?;', array($commentId))) {
			header('HTTP/1.1 204 No Content');
			exit();
		}
		header('HTTP/1.1 500 Internal Server Error');
		exit();
	}
}

