<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Website\UserException;
use VoicesOfWynn\Models\Website\Npc;

class Rating extends WebpageController
{
    private const MOJANG_API_USER_PROFILE_ENDPOINT = 'https://api.mojang.com/user/profile/';
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): int
	{
		$npc = new Npc(['id' => array_shift($args)]);
		$action = array_shift($args);
		
		switch ($action) {
			case '+':
                if (isset($_REQUEST['uuid']) && !$this->verifyUuid($_REQUEST['uuid'])) {
                    return 400;
                }
                $uuid = (empty($_REQUEST['uuid'])) ? null : str_replace('-', '', $_REQUEST['uuid']);
                $voterId = hash('sha256', $uuid ?? $_SERVER['REMOTE_ADDR']);
				if ($npc->wasVotedFor($voterId, '+')) {
                    $npc->resetVote($voterId);
                    return 204;
				}
                $npc->upvote($voterId);
				return 204;
			case '-':
                if (isset($_REQUEST['uuid']) && !$this->verifyUuid($_REQUEST['uuid'])) {
                    return 400;
                }
                $uuid = (empty($_REQUEST['uuid'])) ? null : str_replace('-', '', $_REQUEST['uuid']);
                $voterId = hash('sha256', $uuid ?? $_SERVER['REMOTE_ADDR']);
                if ($npc->wasVotedFor($voterId, '-')) {
                    $npc->resetVote($voterId);
                    return 204;
				}
                $npc->downvote($voterId);
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

    private function verifyUuid(string $uuid) {
        $uuid = str_replace('-', '', $uuid); //trim UUID

        $ch = curl_init(self::MOJANG_API_USER_PROFILE_ENDPOINT . $uuid);

        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        //curl_setopt($ch, CURLOPT_NOBODY, true); Mojang API returns 405 for HEAD request

        curl_exec($ch);

        if(curl_error($ch)) {
            error_log('cURL for verifying Mojang user profile failed with error: ' . curl_error($ch));
            return false;
        }

        return curl_getinfo($ch, CURLINFO_HTTP_CODE) === 200;
    }
}

