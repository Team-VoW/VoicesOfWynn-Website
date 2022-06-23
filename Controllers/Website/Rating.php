<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Controllers\Controller;
use VoicesOfWynn\Models\Recording;
use VoicesOfWynn\Models\UserException;

class Rating extends WebpageController
{
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		$recording = new Recording(array('id' => array_shift($args)));
		$action = array_shift($args);
		
		switch ($action) {
			case '+':
				if ($recording->wasVotedFor('+')) {
                    $recording->resetVote();
                    return 204;
				}
				$recording->upvote();
				return 204;
			case '-':
                if ($recording->wasVotedFor('-')) {
                    $recording->resetVote();
                    return 204;
				}
				$recording->downvote();
				return 204;
			case 'c':
				try {
                    if (isset($_POST['verified']) && $_POST['verified'] === "true") { //HTTP turns JavaScript "true" into string
                        $commentId = $recording->comment(true, null, null, null, $_POST['content'], null, null);
                    }
                    else {
	                    $commentId = $recording->comment(false, $_SERVER['REMOTE_ADDR'], $_POST['name'], $_POST['email'], $_POST['content'], $_SESSION['antispam'], $_POST['antispam']);
                    }
					exit($commentId);
				} catch (UserException $e) {
					header('HTTP/1.1 418 '.$e->getMessage());
					exit(); //TODO replace this
				}
			default:
				return 400;
		}
	}
}

