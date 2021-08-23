<?php

namespace VoicesOfWynn\Models;

class Recording
{
	private int $id = 0;
	private int $npcId = 0;
	private int $questId = 0;
	private int $line = 0;
	private string $file = '';
	private int $upvotes = 0;
	private int $downvotes = 0;
	
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
}

