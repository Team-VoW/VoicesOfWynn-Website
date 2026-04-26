<?php

namespace VoicesOfWynn\Models\Website;

use \JsonSerializable;
use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Storage\Storage;

class Quest implements JsonSerializable
{
	private int $id;
	private ?string $name = null;
    private ?string $degeneratedName = null;
    private ?User $scriptAuthor = null;

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
                    if ($value instanceof User) {
                        $this->scriptAuthor = $value;
                        break;
                    } else if (is_null($value)) {
                        break;
                    }
                    $writer = new User();
                    $writer->setData(['id' => $value]);
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
     * Loads basic quest data by ID from the database.
     * @return static|null The Quest object, or NULL if no quest with the given ID exists
     */
    public static function findById(int $id): ?static
    {
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery(
            'SELECT quest_id, name, degenerated_name FROM quest WHERE quest_id = ?;',
            [$id]
        );
        if ($result === false) {
            return null;
        }
        return new static($result);
    }

    /**
     * Updates the script writer for this quest in the database
     * @param int|null $writerId ID of the user to set as writer, or NULL to clear
     * @return bool Whether the database query was successful
     */
    public function setWriter(?int $writerId): bool
    {
        return (new Db('Website/DbInfo.ini'))->executeQuery(
            'UPDATE quest SET writer = ? WHERE quest_id = ?;',
            [$writerId, $this->id]
        );
    }

    /**
     * Updates the sound editor for a specific NPC in this quest in the database
     * @param int $npcId ID of the NPC whose editor to update
     * @param int|null $editorId ID of the user to set as editor, or NULL to clear
     * @return bool Whether the database query was successful
     */
    public function setNpcEditor(int $npcId, ?int $editorId): bool
    {
        return (new Db('Website/DbInfo.ini'))->executeQuery(
            'UPDATE npc_quest SET editor = ? WHERE quest_id = ? AND npc_id = ?;',
            [$editorId, $this->id, $npcId]
        );
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
        if (is_null($result['writer'])) {
            $this->scriptAuthor = null;
        } else {
            $writer = new User();
            $writer->setData(['id' => $result['writer']]);
            $this->scriptAuthor = $writer;
        }
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
        return isset($this->scriptAuthor) ? $this->scriptAuthor : null;
    }

    /**
     * Returns the URL of this quest's script file, or NULL if no script has been uploaded yet
     */
    public function getScriptUrl(Storage $storage): ?string
    {
        $key = 'scripts/' . $this->degeneratedName . '.txt';
        return $storage->exists($key) ? $storage->getUrl($key) : null;
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
            SELECT 
                npc_id,
                npc.name,
                degenerated_name,
                voice_actor_id AS "va_id",
                voice_actor.display_name AS "va_name",
                voice_actor.picture AS "va_picture",
                npc_quest.editor AS "se_id",
                sound_editor.display_name AS "se_name",
                sound_editor.picture AS "se_picture",
                archived,
                upvotes,
                downvotes,
                (SELECT COUNT(*) FROM comment WHERE npc_id = npc.npc_id) AS comments
            FROM npc
            JOIN npc_quest USING (npc_id)
            LEFT JOIN user voice_actor ON voice_actor.user_id = npc.voice_actor_id
            LEFT JOIN user sound_editor ON sound_editor.user_id = npc_quest.editor
            WHERE quest_id = ?
            ORDER BY npc_quest.sorting_order;
        ', array($this->id), true);

        $this->npcs = [];
        if ($result === false) {
            return false; //Quest with 0 NPCs is not normal
        }
        foreach ($result as $dbRow) {
            $npc = new Npc($dbRow);
            if (!is_null($dbRow['va_id'])) {
                $va = new User();
                $va->setData(['id' => $dbRow['va_id'], 'name' => $dbRow['va_name'], 'avatar' => $dbRow['va_picture']]);
                $npc->setVoiceActor($va);
            }
            if (!is_null($dbRow['se_id'])) {
                $se = new User();
                $se->setData(['id' => $dbRow['se_id'], 'name' => $dbRow['se_name'], 'avatar' => $dbRow['se_picture']]);
                $npc->setSoundEditor($this, $se);
            }
            $this->npcs[] = $npc;
        }
        return true;
    }
}

