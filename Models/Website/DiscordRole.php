<?php

namespace VoicesOfWynn\Models\Website;

use Exception;
use OpenApi\Attributes as OA;
use VoicesOfWynn\Models\Db;

#[OA\Schema(
    schema: "DiscordRole",
    properties: [
        new OA\Property(property: "name", type: "string", description: "Role name"),
        new OA\Property(property: "color", type: "string", description: "Role color (hex code)"),
        new OA\Property(property: "weight", type: "integer", description: "Role weight/priority")
    ]
)]
class DiscordRole
{
	public string $name;
	public string $color;
	public int $weight;
	
	private int $roleId;
	
	public function __construct(string $name, string $color = "FFFFFF", int $weight = 0)
	{
		$this->name = $name;
		$this->color = $color;
		$this->weight = $weight;
	}

    /**
     * Loads and returns the ID of this role based on the name
     * @throws Exception If the name of this Discord role wasn't set
     */
	public function getId()
	{
		if (isset($this->roleId)) { return $this->roleId; }
		if (empty($this->name)) {
			throw new Exception('Attribute $name of the DiscordRole object mustn\'t be empty for the getId() method to be called!');
		}

        $db = new Db('Website/DbInfo.ini');
		$result = $db->fetchQuery('SELECT discord_role_id FROM discord_role WHERE name = ? LIMIT 1', array($this->name));
		if (empty($result)) { return null; }
		$this->roleId = $result['discord_role_id'];
		return $result['discord_role_id'];
	}
}

