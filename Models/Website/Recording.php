<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class Recording
{
	public const IDEAL_COLORS = array(
		'red' => "#CC3333",
		'yellow' => '#CCCC33',
		'green' => '#33CC33',
		'blue' => '#3333CC',
		'purple' => '#CC33CC'
	);
	private const ANTISPAM_TOLLERANCE = 20; //In % out of 256

	private int $id = 0;
	private int $npcId = 0;
	private int $questId = 0;
	private int $line = 0;
	private string $file = '';
	private int $upvotes = 0;
	private int $downvotes = 0;
	private int $comments = 0;
    private bool $archived = false;

	/**
	 * @param array $data Data returned from database, invalid items are skipped, multiple key names are supported for
	 * each attribute
	 */
	public function __construct(array $data)
	{
		foreach ($data as $key => $value) {
			switch ($key) {
				case 'id':
				case 'recording_id':
					$this->id = $value;
					break;
				case 'npcId':
				case 'npc_id':
				case 'npc':
					$this->npcId = $value;
					break;
				case 'questId':
				case 'quest_id':
				case 'quest':
					$this->questId = $value;
					break;
				case 'line':
				case 'number':
				case 'line_number':
					$this->line = $value;
					break;
				case 'file':
				case 'filename':
				case 'fileName':
				case 'recording':
				case 'audio':
					$this->file = $value;
					break;
				case 'upvotes':
				case 'likes':
					$this->upvotes = $value;
					break;
				case 'downvotes':
				case 'dislikes':
					$this->downvotes = $value;
					break;
				case 'comments':
				case 'comment_count':
				case 'commentCount':
					$this->comments = $value;
					break;
                case 'archived':
                case 'hidden':
                    $this->archived = $value;
                    break;
			}
		}
	}

	/**
	 * Generic getter
	 * @param $attr
	 * @return mixed
	 */
	public function __get($attr)
	{
		if (isset($this->$attr)) {
			return $this->$attr;
		}
		return null;
	}

    /**
     * Checks if this recording has been voted for by the client communicating from the current IP
     * @param string $type Either "+" to check for upvotes or "-" to check for downvotes
     * @return bool TRUE if it was, FALSE if it wasn't
     */
    public function wasVotedFor(string $type): bool {
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('
            SELECT COUNT(*) as "cnt" FROM vote WHERE recording_id = ? AND ip = ? AND type = ?
        ', array($this->id, inet_pton($_SERVER['REMOTE_ADDR']), $type));
        return !(($result['cnt'] === 0));
    }

	/**
	 * Upvotes this recording and sets the cookie preventing duplicate votes
	 * @return bool
	 * @throws \Exception
	 */
	public function upvote(): bool
	{
        if ($this->wasVotedFor("-")) {
            //Remove upvote
            (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE vote SET type = "+" WHERE recording_id = ? AND ip = ?;',
                array($this->id, inet_pton($_SERVER['REMOTE_ADDR'])));
        }
        else {
            //Add upvote
            (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO vote(recording_id, ip, type) VALUES (?,?,"+");',
                array($this->id, inet_pton($_SERVER['REMOTE_ADDR'])));
        }

        return $this->updateVotesCounts();
	}

	/**
	 * Downvotes this recording and sets the cookie preventing duplicate votes
	 * @return bool
	 * @throws \Exception
	 */
	public function downvote(): bool
	{
        if ($this->wasVotedFor("+")) {
            //Remove downvote
            (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE vote SET type = "-" WHERE recording_id = ? AND ip = ?;',
                array($this->id, inet_pton($_SERVER['REMOTE_ADDR'])));
        }
        else {
            //Add downvote
            (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO vote(recording_id, ip, type) VALUES (?,?,"-");',
                array($this->id, inet_pton($_SERVER['REMOTE_ADDR'])));
        }

		return $this->updateVotesCounts();
	}

    /**
     * Removes any upvote or downvote on this recording left by the current IP
     * @return bool
     * @throws \Exception
     */
    public function resetVote(): bool
    {
        (new Db('Website/DbInfo.ini'))->executeQuery('DELETE FROM vote WHERE recording_id = ? AND ip = ?',
            array($this->id, inet_pton($_SERVER['REMOTE_ADDR'])));
        return $this->updateVotesCounts();
    }

    /**
     * Updates upvote/downvote count for this recording in the database
     * @return bool
     * @throws \Exception
     */
    private function updateVotesCounts()
    {
        return (new Db('Website/DbInfo.ini'))->executeQuery('
            UPDATE recording SET
            upvotes = (SELECT COUNT(*) FROM vote WHERE recording_id = ? AND type = "+"),
            downvotes = (SELECT COUNT(*) FROM vote WHERE recording_id = ? AND type = "-")
            WHERE recording_id = ?;
            ', array($this->id, $this->id, $this->id));
    }

	/**
	 * Adds a new comment to this recording
     * @param $verified bool TRUE, if the user is posting as an contributor (verification if anyone is actually logged in will be performed), FALSE, if they're posting as a guest
	 * @param $ip string|null
	 * @param $author string|null
	 * @param $email string|null
	 * @param $content string
	 * @param $antispam string|null
	 * @return int ID of the newly created comment
	 * @throws \Exception
	 */
	public function comment(bool $verified, $ip, $author, $email, $content, $antispamQuestion, $antispamAnswer)
	{
		if (!$verified) {
			$idealColor = self::IDEAL_COLORS[$antispamQuestion];
			$redPart = hexdec(substr($idealColor, 1, 2));
			$greenPart = hexdec(substr($idealColor, 3, 2));
			$bluePart = hexdec(substr($idealColor, 5, 2));
			$absoluteTollerance = round(256 * self::ANTISPAM_TOLLERANCE / 100);

			$redPartAnswer = hexdec(substr($antispamAnswer, 1, 2));
			$greenPartAnswer = hexdec(substr($antispamAnswer, 3, 2));
			$bluePartAnswer = hexdec(substr($antispamAnswer, 5, 2));

			if (
				$redPartAnswer + $absoluteTollerance < $redPart || $redPartAnswer - $absoluteTollerance > $redPart ||
				$greenPartAnswer + $absoluteTollerance < $greenPart || $greenPartAnswer - $absoluteTollerance > $greenPart ||
				$bluePartAnswer + $absoluteTollerance < $bluePart || $bluePartAnswer - $absoluteTollerance > $bluePart
			) {
				throw new UserException('The colour you picked was too distinct from '.$antispamQuestion.'. Try again please.');
			}
		}

        if ($verified) {
            if (!isset($_SESSION['user'])) {
                throw new UserException('No contributor is logged in.');
            }
            $userId = $_SESSION['user']->getId();
	        $ip = null;
	        $author = null;
	        $email = null;
        }
        else {
            $author = trim($author);
            if (empty($author)) {
                $author = 'Anonymous';
            }
            if (mb_strlen($author) > 31) {
                throw new UserException('Name is too long, 31 characters is the limit.');
            }

	        $email = trim($email);
	        if (empty($email)) {
		        $email = ""; //NULL would mess up with the SQL MD5 function used inside the CONCAT function
	        }
	        if (mb_strlen($email) > 255) {
		        throw new UserException('E-mail is too long, 255 characters is the limit.');
	        }
	        //Check e-mail format (might not allow some exotic but valid e-mail domains)
	        if ($email !== "" && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		        throw new UserException('E-mail address doesn\'t seem to be in the correct format. If you are sure that you entered your e-mail address properly, ping Shady#2948 on Discord.');
	        }

	        $userId = null;
        }

		$content = trim($content);
		if (empty($content)) {
			throw new UserException('No content submitted');
		}
		if (mb_strlen($author) > 31) {
			throw new UserException('Name is too long, 31 characters is the limit.');
		}
		if (mb_strlen($email) > 255) {
			throw new UserException('E-mail is too long, 255 characters is the limit.');
		}
		if (strlen($content) > 65535) { //Not using mb_strlen, because we need to count single-bit characters
			throw new UserException('Comment is too long, 65,535 characters is the limit.');
		}

		$badwords = file('Models/BadWords.txt');
		foreach ($badwords as $badword) {
			if (mb_stripos($content, trim($badword)) !== false) {
				throw new UserException('The comment contains a bad word: "'.trim($badword).'". If you believe that it\'s not used as a profanity, join our Discord (link in the footer) and ping Shady#2948.');
			}
		}

		return (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO comment (verified,user_id,ip,name,email,content,recording_id) VALUES (?,?,?,?,?,?,?);', array(
			$verified,
            $userId,
			inet_pton($ip),
            $author,
			$email,
			$content,
			$this->id
		), true);
	}

    /**
     * Method archiving this recording by marking it as archived in the database and renaming the recording file
     * @param string $prefix Custom prefix to be appended to the current file name of the recording file; "_archived" as default
     * @return bool Whether the database query was executed successfully
     */
    public function archive(string $prefix = '_archived') : bool
    {
        //Rename the file
        rename('dynamic/recordings/'.$this->file, 'dynamic/recordings/'.$prefix.$this->file);

        //Change the attributes
        $this->archived = true;
        $this->file = $prefix.$this->file;

        //Update the database
        $db = new Db('Website/DbInfo.ini');
        return $db->executeQuery('UPDATE recording SET archived = TRUE, file = ? WHERE recording_id = ?;', array($this->file, $this->id));
    }
}

