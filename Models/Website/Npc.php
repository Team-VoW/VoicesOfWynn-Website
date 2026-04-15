<?php

namespace VoicesOfWynn\Models\Website;

use \JsonSerializable;
use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Storage\Storage;
use function VoicesOfWynn\storageUrl;

class Npc implements JsonSerializable
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
	private string $name = '';
    private string $degeneratedName = '';
	private $voiceActor;
    private bool $archived = false;
    private int $upvotes = 0;
    private int $downvotes = 0;
    private int $comments = 0;

	private array $recordings;

    private int $recordingsCount = 0;

	/**
	 * @param array $data Data returned from database, invalid items are skipped, multiple key names are supported for
	 * each attribute
	 */
	public function __construct(array $data)
	{
		foreach ($data as $key => $value) {
			switch ($key) {
				case 'id':
				case 'npc_id':
					$this->id = $value;
					break;
				case 'name':
				case 'nname':
				case 'npc_name':
					$this->name = $value;
					break;
                case 'dname':
                case 'degenerated_name':
                    $this->degeneratedName = $value;
                    break;
                case 'archived':
                case 'hidden':
                    $this->archived = $value;
                    break;
                case 'recordings_count':
                    $this->recordingsCount = $value;
                    break;
                case 'upvotes':
                    $this->upvotes = $value;
                    break;
                case 'downvotes':
                    $this->downvotes = $value;
                    break;
                case 'comments':
                    $this->comments = $value;
                    break;
			}
		}
	}

	public function jsonSerialize() : mixed
	{
	    return (object) get_object_vars($this);
	}

	/**
	 * VoiceActor setter
	 * @param User $voiceActor
	 */
	public function setVoiceActor(User $voiceActor) : void
	{
		$this->voiceActor = $voiceActor;
	}

    /**
     * Method setting a new voice actor and updating the database
     * @param User $voiceActor
     * @return bool Whether the database query has successfully been executed
     */
    public function recast(User $voiceActor) : bool
    {
        $this->setVoiceActor($voiceActor);

        //Update the database
        return (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE npc SET voice_actor_id = ? WHERE npc_id = ? LIMIT 1;', array($voiceActor->getId(), $this->id));
    }


    /**
     * Checks if this NPC has been voted for by certain client
     * @param string $voterId SHA256 hash of either Minecraft user UUID or IP address of the user whose voting we're checking
     * @param string $type Either "+" to check for upvotes or "-" to check for downvotes
     * @return bool TRUE if it was, FALSE if it wasn't
     */
    public function wasVotedFor(string $voterId, string $type): bool
    {
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('
            SELECT COUNT(*) as "cnt" FROM vote WHERE npc_id = ? AND voter = ? AND type = ?
        ', array($this->id, $voterId, $type));
        return !(($result['cnt'] === 0));
    }

    /**
     * Upvotes this NPC
     * @param string $voterId SHA256 hash of either Minecraft user UUID or IP address of the user whose vote we're saving
     * @return bool
     * @throws \Exception
     */
    public function upvote($voterId): bool
    {
        //Either convert downvote to upvote or insert new upvote
        (new Db('Website/DbInfo.ini'))->executeQuery(
            'INSERT INTO vote(npc_id, voter, type) VALUES (?,?,"+") ON DUPLICATE KEY UPDATE type = "+";',
            array($this->id, $voterId)
        );

        return $this->updateVotesCounts();
    }

    /**
     * Downvotes this NPC
     * @param string $voterId SHA256 hash of either Minecraft user UUID or IP address of the user whose vote we're saving
     * @return bool
     * @throws \Exception
     */
    public function downvote($voterId): bool
    {
        //Either convert upvote to downvote or insert new downvote
        (new Db('Website/DbInfo.ini'))->executeQuery(
            'INSERT INTO vote(npc_id, voter, type) VALUES (?,?,"-") ON DUPLICATE KEY UPDATE type = "-";',
            array($this->id, $voterId)
        );

        return $this->updateVotesCounts();
    }

    /**
     * Removes any upvote or downvote on this NPC left by the client identified by UUID in $_REQUEST
     * @param string $voterId SHA256 hash of either Minecraft user UUID or IP address of the user whose vote we're resetting
     * @return bool
     * @throws \Exception
     */
    public function resetVote($voterId): bool
    {
        (new Db('Website/DbInfo.ini'))->executeQuery(
            'DELETE FROM vote WHERE npc_id = ? AND voter = ?',
            array($this->id, $voterId)
        );
        return $this->updateVotesCounts();
    }

    /**
     * Updates upvote/downvote count for this NPC in the database
     * @return bool
     * @throws \Exception
     */
    private function updateVotesCounts()
    {
        return (new Db('Website/DbInfo.ini'))->executeQuery('
            UPDATE npc SET
            upvotes = (SELECT COUNT(*) FROM vote WHERE npc_id = ? AND type = "+"),
            downvotes = (SELECT COUNT(*) FROM vote WHERE npc_id = ? AND type = "-")
            WHERE npc_id = ?;
            ', array($this->id, $this->id, $this->id));
    }

    public function getUpvotes() : int
    {
        return $this->upvotes;
    }

    public function getDownvotes() : int
    {
        return $this->downvotes;
    }

    /**
     * Adds a new comment to this NPC
     * Also sends the comment to the Discord webhook
     * @param $verified bool TRUE, if the user is posting as a contributor (verification if anyone is actually logged in will be performed), FALSE, if they're posting as a guest
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
                throw new UserException('The colour you picked was too distinct from ' . $antispamQuestion . '. Try again please. If you are colorblind, please let us know on Discord by sending a DM to any admin.');
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
        } else {
            $author = trim($author);
            if (empty($author)) {
                $author = 'Anonymous';
            }
            if (mb_strlen($author) > 31) {
                throw new UserException('Name is too long, 31 characters is the limit.');
            }

            $email = trim($email);
            if (empty($email)) {
                $email = ""; //NULL would mess up the SQL MD5 function used inside the CONCAT function
            }
            if (mb_strlen($email) > 255) {
                throw new UserException('E-mail is too long, 255 characters is the limit.');
            }
            //Check e-mail format (might not allow some exotic but valid e-mail domains)
            if ($email !== "" && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
                throw new UserException('E-mail address doesn\'t seem to be in the correct format. If you are sure that you entered your e-mail address properly, ping shady_medic on Discord.');
            }

            $userId = null;
        }

        $content = trim($content);
        if (empty($content)) {
            throw new UserException('No content submitted');
        }
        if (strlen($content) > 65535) { //Not using mb_strlen, because we need to count single-bit characters
            throw new UserException('Comment is too long, 65,535 characters is the limit.');
        }

        //Save the comment
        $commentId = (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO comment (verified,user_id,ip,name,email,content,npc_id) VALUES (?,?,?,?,?,?,?);', array(
            (int) $verified,
            $userId,
            ($ip === null) ? null : inet_pton($ip),
            $author,
            $email,
            $content,
            $this->id
        ), true);

        //Construct the object to easily get name and avatar for the webhook message
        $comment = new Comment(array(
            'id' => $commentId,
            'verified' => $verified,
            'userId' => $userId,
            'ip' => $ip,
            'author' => $author,
            'email' => $email,
            'content' => $content,
            'npcId' => $this->id
        ));

        //Comment couldn't be saved
        if ($commentId === false) {
            return false;
        }

        //Forward to the webhook
        $this->loadName(); //Load name for use in the message
        $commentLines = preg_split("/\r\n|\n|\r/", $content); //Copied from https://stackoverflow.com/a/11165332/14011077
        $discordMessage = 'New comment has been posted on the following NPC: `' . $this->name . ' (ID #' . $this->id . ')`';
        foreach ($commentLines as $commentLine) {
            $discordMessage .= "\n> " . htmlspecialchars(trim($commentLine));
        }
        $discordMessage .= "\n\n".'View the comment at https://' . $_SERVER['SERVER_NAME'] . '/contents/npc/' . $this->id . '/comments' . '#c' . $commentId . '.';

        $webhookResult = $this->sendWebhookMessage($discordMessage, $comment->getName() . ' via voicesofwynn.com', $comment->getAvatar());

        return $webhookResult ? $commentId : false;
    }

    /**
     * Method forwarding the message to our Discord server via webhook
     * @param string $message Message that was posted
     * @param string|null $username Username of the poster (either their account username or whatever they filled into the relevant field)
     * @param string|null $avatar Avatar of the poster (either their account avatar, or the Gravatar image generated from their e-mail, if they used one)
     * @return bool TRUE on success, FALSE on failure
     */
    private function sendWebhookMessage(string $message, ?string $username = null, ?string $avatar = null)
    {
        $curl = curl_init(getenv('DISCORD_COMMENTS_WEBHOOK_URL'));
        curl_setopt($curl, CURLOPT_POST, true);

        $headers = array(
            "Content-Type: application/json",
        );
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);

        $data = json_encode([
            'content' => $message,
            'username' => $username,
            'avatar_url' => $avatar
        ], JSON_UNESCAPED_SLASHES);

        curl_setopt($curl, CURLOPT_POSTFIELDS, $data);

        $result = curl_exec($curl);
        curl_close($curl);
        return $result;
    }

    public function getCommentsCount() : int
    {
        return $this->comments;
    }

    /**
     * Method archiving this whole NPC by unlinking it from all quests, archiving all its recordings and replacing
     * its position in quests with a newly created NPC with the same name and profile picture.
     * @return int ID of the newly created NPC
     */
    public function archive() : int
    {
        $db = new Db('Website/DbInfo.ini');

        //Archive all recordings
        $questIdsRaw = $db->fetchQuery('SELECT quest_id FROM npc_quest WHERE npc_id = ?;', array($this->id), true);
        $questIds = array();
        foreach($questIdsRaw as $questId) {
            $this->archiveQuestRecordings($questId['quest_id']);
            $questIds[] = $questId['quest_id'];
        }

        //Create new NPC
        $this->loadName(); //Needed to be copied
        $replacementId = $db->executeQuery('INSERT INTO npc(name, degenerated_name) VALUES (?,?);', array($this->name, $this->degeneratedName), true);
        $replacementNpc = new Npc(array('id' => $replacementId, 'name' => $this->name));
        unset($replacementId);

        //Copy profile picture
        Storage::get()->copy('npcs/'.$this->id.'.webp', 'npcs/'.$replacementNpc->getId().'.webp');

        //Unlink this NPC from all quests and link the new one
        $inString = trim(str_repeat('?,', count($questIds)), ',');
        $parameters = $questIds;
        array_unshift($parameters, $replacementNpc->id);
        array_push($parameters, $this->id);
        $db->executeQuery('UPDATE npc_quest SET npc_id = ? WHERE quest_id IN ('.$inString.') AND npc_id = ?;', $parameters);

        //Set this NPC as archived in the database and in the property
        $db->executeQuery('UPDATE npc SET archived = TRUE WHERE npc_id = ?;', array($this->id));
        $this->archived = true;

        return $replacementNpc->getId();
    }

    /**
     * Method archiving all recordings of this NPC for a specific quest.
     * This is useful when a quest receives a large revamp and the voice actor does a full revoicing of the dialogue
     * @param int $questId ID of the quest whose recordings should be affected
     * @return bool Whether everything was updated successfully
     */
    public function archiveQuestRecordings(int $questId) : bool
    {
        //Get IDs of all recordings of this NPC in the quest
        $db = new Db('Website/DbInfo.ini');
        $result = $db->fetchQuery('SELECT recording_id, file FROM recording WHERE npc_id = ? AND quest_id = ? AND archived = 0;', array($this->id, $questId), true);

        //Archive the recordings
        foreach ($result as $recordingData) {
            $recording = new Recording(array('id' => $recordingData['recording_id'], 'file' => $recordingData['file']));
            $prefix = '!archived_' . date('Y-m-d') . '_';
            if (!$recording->archive($prefix)) {
                return false;
            }
        }

        return true;
    }

	/**
	 * ID getter
	 * @return int|null ID of this NPC or NULL if it wasn't set
	 */
	public function getId() : ?int
	{
		return $this->id;
	}

    /**
     * Method loading the name and degenerated name of this NPC from the database.
     * The ID property must be filled for this method to work
     * @return bool TRUE on success, FALSE on failure (ID is not known or no database results)
     */
    private function loadName() : bool
    {
        if (empty($this->id)) {
            return false;
        }

        $db = new Db('Website/DbInfo.ini');
        $result = $db->fetchQuery('SELECT name, degenerated_name FROM npc WHERE npc_id = ?;', array($this->id));

        if (!empty($result)) {
            $this->name = $result['name'];
            $this->degeneratedName = $result['degenerated_name'];
            return true;
        }
        return false;
    }

	/**
	 * Name getter
	 * @return string|null Name of this NPC or NULL if it wasn't set
	 */
	public function getName() : ?string
	{
		return $this->name;
	}

    /**
     * Degenerated name getter
     * @return string|null Degenerated name of this NPC (without spaces and special chars) or NULL if it wasn't set
     */
    public function getDegeneratedName() : ?string
    {
        return $this->degeneratedName;
    }

	/**
	 * VoiceActor getter
	 * @return User|null Object representing the user voicing this NPC or NULL if it wasn't set
	 */
	public function getVoiceActor() : ?User
	{
		return $this->voiceActor;
	}

    public function isArchived() : bool
    {
        return $this->archived;
    }

	/**
	 * Method adding a Recording object to this NPC's $recordings attribute
	 * @param Recording $recording The Recording object to add
	 */
	public function addRecording(Recording $recording) : void
	{
		$this->recordings[] = $recording;
	}

    /**
     * Recordings getter
     * @param Quest|null $quest Quest to filter by
     * @return array
     */
	public function getRecordings(Quest $quest = null) : array
	{
        if (!isset($this->recordings)) {
            $this->loadRecordings();
        }
        return (is_null($quest)) ? $this->recordings : array_filter($this->recordings, function ($recording) use ($quest) { return $recording->questId === $quest->getId(); });;
	}

    private function loadRecordings() : bool
    {
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('
            SELECT recording_id,npc_id,quest_id,line,file,archived
            FROM recording
            WHERE npc_id = ?
            ORDER BY line;
        ', array($this->id), true);

        $this->recordings = [];
        if ($result === false) {
            return false; //NPC with 0 recordings is not normal (in cases when NPC just didn't have its recordings uploaded yet, the return value shouldn't cause any problems anyway
        }
        foreach ($result as $dbRow) {
            $this->recordings[] = new Recording($dbRow);
        }
        return true;
    }

    /**
     * Recordings count getter
     * @return int
     */
    public function getRecordingsCount(): int
    {
        return $this->recordingsCount;
    }

    /**
     * Gets a suitable "pick" from this NPCs' recordings in a single quest.
     * Returns the first recording ordered by line number.
     * @param Quest $quest Quest to filter by
     * @return Recording|null
     */
    public function getSampleRecording(Quest $quest): ?Recording
    {
        $recordings = array_values($this->getRecordings($quest));
        return $recordings[0] ?? null;
    }
}

