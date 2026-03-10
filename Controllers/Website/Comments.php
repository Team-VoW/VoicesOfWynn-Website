<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Website\ContentManager;
use VoicesOfWynn\Models\Website\Npc;

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
	 * @return int
	 */
	private function get($args): int
	{
		$isDialog = ($args[1] ?? null) === 'dialog';
		$prefix = $isDialog ? 'comments_dialog_' : 'comments_';

		self::$data[$prefix.'admin']     = isset($_SESSION['user']) && $_SESSION['user']->isSysAdmin();
		self::$data[$prefix.'logged_in'] = isset($_SESSION['user']);
		if (isset($_SESSION['user'])) {
			self::$data[$prefix.'user_id']     = $_SESSION['user']->getId();
			self::$data[$prefix.'user_name']   = $_SESSION['user']->getName();
			self::$data[$prefix.'user_avatar'] = $_SESSION['user']->getAvatarLink();
		}

		$npcId = $args[0];
		$cnm = new ContentManager();
		$npc = $cnm->getNpc($npcId);
		if ($npc === false) {
			return 404;
		}

		$voiceActor = $npc->getVoiceActor();
		self::$data[$prefix.'voice_actor_id']   = $voiceActor ? $voiceActor->getId() : 0;
		self::$data[$prefix.'comments']         = $cnm->getComments($npcId);
		self::$data[$prefix.'owned_comments']   = $cnm->getOwnedComments($npcId);
		$color = ['red', 'yellow', 'green', 'blue', 'purple'][rand(0, 4)];
		$_SESSION['antispam'] = $color;
		self::$data[$prefix.'antispam_color']      = $color;
		self::$data[$prefix.'antispam_color_code'] = Npc::IDEAL_COLORS[$color];

		if ($isDialog) {
			self::$data[$prefix.'npcId'] = $npcId;
			self::$views = ['comments_dialog'];
			return 200;
		}

		self::$data['base_title']       = 'Comments';
		self::$data['base_description'] = 'Read all the comments posted on this recording or post a new one. Praise the voice actor for their performance or suggest what could be improved.';
		self::$data['base_keywords']    = 'Minecraft,Wynncraft,Mod,Voice,Contents,Content,Recording,Comments,Feedback';
		self::$data[$prefix.'npc']      = $npc;
		self::$cssFiles[] = 'audio-player';
		self::$cssFiles[] = 'comments';
		self::$jsFiles[]  = 'audio-player';
		self::$jsFiles[]  = 'comments';
		self::$jsFiles[]  = 'md5';
		self::$views[]    = 'comments';
		return 200;
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
			(inet_pton($_SERVER['ip']) !== $commentAuthorIp) //Client (by IP) not the author of the comment
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

