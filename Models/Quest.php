<?php

namespace VoicesOfWynn\Models;

class Quest
{
	private int $id;
	private string $name;
	private array $npcs = array();
	
	/**
	 * @param array $data Data returned from database, invalid items are skipped, multiple key names are supported for
	 * each attribute
	 */
	public function __construct(array $data)
	{
		foreach ($data as $key => $value) {
			switch ($key) {
				case 'id':
				case 'quest_id':
					$this->id = $value;
					break;
				case 'name':
				case 'qname':
				case 'quest_name':
					$this->name = $value;
					break;
			}
		}
	}
	
	/**
	 * Method adding a NPC object to this quest's $npcs attribute
	 * @param Npc $npc The NPC object to add
	 */
	public function addNpc(Npc $npc)
	{
		$this->npcs[] = $npc;
	}
	
	/**
	 * ID getter
	 * @return int|null ID of this quest or NULL if it wasn't set
	 */
	public function getId()
	{
		return $this->id;
	}
	
	/**
	 * Name getter
	 * @return string|null Name of this quest or NULL if it wasn't set
	 */
	public function getName()
	{
		return $this->name;
	}
	
	/**
	 * Npcs getter
	 * @return array Array of NPC objects that were added to this quest
	 */
	public function getNpcs(): array
	{
		return $this->npcs;
	}
}

