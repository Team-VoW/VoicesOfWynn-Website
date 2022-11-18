<?php

namespace VoicesOfWynn\Models\Website;

use \JsonSerializable;
use VoicesOfWynn\Models\Db;

class Npc implements JsonSerializable
{
	private int $id = 0;
	private string $name = '';
	private $voiceActor;
	
	private array $recordings = array();
	
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
	 * ID getter
	 * @return int|null ID of this NPC or NULL if it wasn't set
	 */
	public function getId() : ?int
	{
		return $this->id;
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
	 * VoiceActor getter
	 * @return User|null Object representing the user voicing this NPC or NULL if it wasn't set
	 */
	public function getVoiceActor() : ?User
	{
		return $this->voiceActor;
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
}

