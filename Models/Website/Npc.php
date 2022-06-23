<?php

namespace VoicesOfWynn\Models\Website;

class Npc
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
	
	/**
	 * VoiceActor setter
	 * @param User $voiceActor
	 */
	public function setVoiceActor(User $voiceActor)
	{
		$this->voiceActor = $voiceActor;
	}
	
	/**
	 * ID getter
	 * @return int|null ID of this NPC or NULL if it wasn't set
	 */
	public function getId()
	{
		return $this->id;
	}
	
	/**
	 * Name getter
	 * @return string|null Name of this NPC or NULL if it wasn't set
	 */
	public function getName()
	{
		return $this->name;
	}
	
	/**
	 * VoiceActor getter
	 * @return User|null Object representing the user voicing this NPC or NULL if it wasn't set
	 */
	public function getVoiceActor()
	{
		return $this->voiceActor;
	}
	
	/**
	 * Method adding a Recording object to this NPC's $recordings attribute
	 * @param Recording $recording The Recording object to add
	 */
	public function addRecording(Recording $recording)
	{
		$this->recordings[] = $recording;
	}
	
	/**
	 * Recordings getter
	 * @return array
	 */
	public function getRecordings()
	{
		return $this->recordings;
	}
}

