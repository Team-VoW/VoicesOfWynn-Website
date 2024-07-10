<?php

namespace VoicesOfWynn\Models\Website;

use \JsonSerializable;
use VoicesOfWynn\Models\Db;

class Npc implements JsonSerializable
{
	private int $id = 0;
	private string $name = '';
    private string $degeneratedName = '';
	private $voiceActor;
    private bool $archived = false;

	private array $recordings = array();

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
			}
		}
	}

	public function jsonSerialize() : object
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
        copy('dynamic/npcs/'.$this->id.'.png', 'dynamic/npcs/'.$replacementNpc->getId().'.png');

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
	 * @return array
	 */
	public function getRecordings() : array
	{
		return $this->recordings;
	}

    /**
     * Recordings count getter
     * @return int
     */
    public function getRecordingsCount(): int
    {
        return $this->recordingsCount;
    }
}

