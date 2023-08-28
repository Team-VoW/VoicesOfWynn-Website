<?php

namespace VoicesOfWynn\Models\Website;

use \JsonSerializable;

class Quest implements JsonSerializable
{
	private int $id;
	private string $name;
    private string $degeneratedName;
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
                case 'dname':
                case 'degenerated_name':
                    $this->degeneratedName = $value;
                    break;
			}
		}
	}
	
	public function jsonSerialize()
	{
	    return (object) get_object_vars($this);
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
	public function getId() : ?int
	{
		return $this->id;
	}
	
	/**
	 * Name getter
	 * @return string|null Name of this quest or NULL if it wasn't set
	 */
	public function getName() : ?string
	{
		return $this->name;
	}

    /**
     * Degenerated name getter
     * @return string|null Degenerated name of this quest (without spaces and special chars) or NULL, if it wasn't set
     */
    public function getDegeneratedName() : ?string
    {
        return $this->degeneratedName;
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

