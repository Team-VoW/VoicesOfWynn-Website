<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Website\UserException;
use VoicesOfWynn\Models\Website\Npc;

class Rating extends WebpageController
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		$npc = new Npc(['id' => array_shift($args)]);
		$action = array_shift($args);
		
		switch ($action) {
			case '+':
				if ($npc->wasVotedFor('+')) {
                    $npc->resetVote();
                    return 204;
				}
                $npc->upvote();
				return 204;
			case '-':
                if ($npc->wasVotedFor('-')) {
                    $npc->resetVote();
                    return 204;
				}
                $npc->downvote();
				return 204;
			case 'c':
				try {
                    if (isset($_POST['verified']) && $_POST['verified'] === "true") { //HTTP turns JavaScript "true" into string
                        $commentId = $npc->comment(true, null, null, null, $_POST['content'], null, null);
                    }
                    else {
	                    $commentId = $npc->comment(false, $_SERVER['REMOTE_ADDR'], $_POST['name'], $_POST['email'], $_POST['content'], $_SESSION['antispam'], $_POST['antispam']);
                    }
					exit($commentId); //TODO Not ideal
				} catch (UserException $e) {
					header('HTTP/1.1 418 '.$e->getMessage());
					exit(); //TODO replace this
				}
			default:
				return 400;
		}
	}
}

