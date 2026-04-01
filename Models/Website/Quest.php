<?php

namespace VoicesOfWynn\Models\Website;

use \JsonSerializable;
use VoicesOfWynn\Models\Db;

class Quest implements JsonSerializable
{
	private int $id;
	private ?string $name = null;
    private ?string $degeneratedName = null;
    private User $scriptAuthor;
	private array $npcs;
	
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
                case 'writer':
                case 'scriptAuthor':
                case 'script_author':
                    if (gettype($value) === 'integer') {
                        $writer = new User();
                        $writer->setData(['id' => $value]);
                    } else {
                        $writer = $value;
                    }
                    $this->scriptAuthor = $writer;
                    break;
			}
		}
	}
	
	public function jsonSerialize() : mixed
	{
	    return (object) get_object_vars($this);
	}

    /**
     * Method loading quest ID and name from the database, filtering by the degenerated name that is already set
     * @return bool TRUE if data was loaded, FALSE if not (quest having the set degenerated name couldn't be found)
     */
    public function loadFromDegeneratedName() : bool
    {
        if (!isset($this->degeneratedName)) {
            throw new \BadMethodCallException('Method Quest::load mustn\'t be called when the Quest::degeneratedName attribute is not set.');
        }

        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('
            SELECT quest_id, name, writer
            FROM quest
            WHERE degenerated_name = ?;
        ', array($this->degeneratedName));

        if ($result === false) {
            return false;
        }
        $this->id = $result['quest_id'];
        $this->name = $result['name'];
        $writer = new User();
        $writer->setData(['id' => $result['writer']]);
        $this->scriptAuthor = $writer;

        return true;
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
     * Script author getter
     * @return User|null User who is the writer of the script for this quest
     */
    public function getScriptAuthor() : ?User
    {
        return $this->scriptAuthor;
    }

    /**
     * Npcs getter
     * @param bool $loadIfNotLoaded Should the list of NPCs be loaded from the database if it is unset at the time this method is called?
     * @return ?array Array of NPC objects that were added to this quest or NULL if the list wasn't set or loaded yet
     */
	public function getNpcs(bool $loadIfNotLoaded = false): ?array
	{
        if (!isset($this->npcs) && $loadIfNotLoaded) {
            $this->loadNpcs();
        }
		return $this->npcs ?? null;
	}

    private function loadNpcs() : bool
    {
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('
            SELECT npc_id,npc.name,degenerated_name,voice_actor_id,user.display_name,user.picture,archived,upvotes,downvotes,(SELECT COUNT(*) FROM comment WHERE npc_id = npc.npc_id) AS comments,sorting_order
            FROM npc
            JOIN npc_quest USING (npc_id)
            JOIN user ON user.user_id = npc.voice_actor_id
            WHERE quest_id = ?
            ORDER BY sorting_order;
        ', array($this->id), true);

        $this->npcs = [];
        if ($result === false) {
            return false; //Quest with 0 NPCs is not normal
        }
        foreach ($result as $dbRow) {
            $npc = new Npc($dbRow);
            $va = new User();
            $va->setData($dbRow);
            $npc->setVoiceActor($va);
            $this->npcs[] = $npc;
        }
        return true;
    }
}

