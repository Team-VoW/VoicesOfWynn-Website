<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Storage\Storage;

class Recording
{

	private int $id = 0;
	private int $npcId = 0;
	private int $questId = 0;
	private int $line = 0;
	private string $file = '';
	private bool $archived = false;

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
				case 'archived':
				case 'hidden':
					$this->archived = $value;
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

	/**
	 * Method archiving this recording by marking it as archived in the database and renaming the recording file
	 * @param string $prefix Custom prefix to be appended to the current file name of the recording file; "_archived" as default
	 * @return bool Whether the database query was executed successfully
	 */
	public function archive(string $prefix = '_archived'): bool
	{
		//Rename the file
		Storage::get()->rename('recordings/' . $this->file, 'recordings/' . $prefix . $this->file);

		//Change the attributes
		$this->archived = true;
		$this->file = $prefix . $this->file;

		//Update the database
		$db = new Db('Website/DbInfo.ini');
		return $db->executeQuery('UPDATE recording SET archived = TRUE, file = ? WHERE recording_id = ?;', array($this->file, $this->id));
	}
}
