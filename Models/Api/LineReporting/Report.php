<?php

namespace VoicesOfWynn\Models\Api\LineReporting;

use DateTime;
use Exception;
use JsonSerializable;
use VoicesOfWynn\Models\Db;
use OpenApi\Attributes as OA;

#[OA\Schema(
    schema: "Report",
    description: "A report containing information about an unvoiced line",
    properties: [
        new OA\Property(property: "ID", type: "integer", description: "Unique identifier for the report"),
        new OA\Property(property: "message", type: "string", description: "The chat message/line content"),
        new OA\Property(property: "NPC", type: "string", description: "Name of the NPC", nullable: true),
        new OA\Property(property: "X", type: "string", description: "X coordinate", nullable: true),
        new OA\Property(property: "Y", type: "string", description: "Y coordinate", nullable: true),
        new OA\Property(property: "Z", type: "string", description: "Z coordinate", nullable: true),
        new OA\Property(property: "reporter", type: "string", description: "Name of the player who reported this line"),
        new OA\Property(property: "last reported", type: "string", description: "Date and time when this was last reported", format: "datetime"),
        new OA\Property(property: "times reported", type: "integer", description: "Number of times this line was reported"),
        new OA\Property(property: "status", type: "string", description: "Current status of the report", enum: ["unprocessed", "forwarded", "accepted", "rejected", "fixed", "draft", "missing"])
    ]
)]

#[OA\Schema(
    schema: "UnvoicedLineReport",
    description: "Simplified report structure for unvoiced line listings",
    properties: [
        new OA\Property(property: "message", type: "string", description: "The chat message/line content"),
        new OA\Property(property: "NPC", type: "string", description: "Name of the NPC", nullable: true),
        new OA\Property(property: "X", type: "string", description: "X coordinate", nullable: true),
        new OA\Property(property: "Y", type: "string", description: "Y coordinate", nullable: true),
        new OA\Property(property: "Z", type: "string", description: "Z coordinate", nullable: true)
    ]
)]
class Report implements JsonSerializable
{
    private int $id;
    private DateTime $submitted;
    private string $chatMessage;
    private ?string $npcName;
    private string $player;
    private ?string $posX;
    private ?string $posY;
    private ?string $posZ;
    private string $reportedTimes;
    private string $status;

    public function load(array $reportInfo)
    {
        foreach ($reportInfo as $key => $value) {
            switch ($key) {
                case 'report_id':
                    $this->id = $value;
                    break;
                case 'time_submitted':
                    $this->submitted = new DateTime($value);
                    break;
                case 'chat_message':
                    $this->chatMessage = $value;
                    break;
                case 'npc_name':
                    $this->npcName = $value;
                    break;
                case 'player':
                    $this->player = $value;
                    break;
                case 'pos_x':
                    $this->posX = $value;
                    break;
                case 'pos_y':
                    $this->posY = $value;
                    break;
                case 'pos_z':
                    $this->posZ = $value;
                    break;
                case 'reported_times':
                    $this->reportedTimes = $value;
                    break;
                case 'status':
                    $this->status = $value;
                    break;
            }
        }
    }

    public function jsonSerialize() : mixed
    {
        $properties = array();

        if (isset($this->id)) {
            $properties['ID'] = $this->id;
        }
        if (isset($this->chatMessage)) {
            $properties['message'] = $this->chatMessage;
        }
        if (isset($this->npcName)) {
            $properties['NPC'] = $this->npcName;
        }
        if (isset($this->posX)) {
            $properties['X'] = $this->posX;
        }
        if (isset($this->posY)) {
            $properties['Y'] = $this->posY;
        }
        if (isset($this->posZ)) {
            $properties['Z'] = $this->posZ;
        }
        if (isset($this->player)) {
            $properties['reporter'] = $this->player;
        }
        if (isset($this->submitted)) {
            $properties['last reported'] = $this->submitted->format('Y-m-d H:i:s');
        }
        if (isset($this->reportedTimes)) {
            $properties['times reported'] = $this->reportedTimes;
        }
        if (isset($this->status)) {
            $properties['status'] = $this->status;
        }

        return $properties;
    }

    public function reset()
    {
        if (empty($this->chatMessage)) {
            throw new Exception("Too little information is known about this report to perform any action");
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');
        $result = $db->executeQuery('UPDATE report SET status = "unprocessed" WHERE chat_message = ? LIMIT 1', array($this->chatMessage));
        return ($result) ? 204 : 500;
    }

    public function undecide()
    {
        if (empty($this->chatMessage)) {
            throw new Exception("Too little information is known about this report to perform any action");
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');
        $result = $db->executeQuery('UPDATE report SET status = "forwarded" WHERE chat_message = ? LIMIT 1', array($this->chatMessage));
        return ($result) ? 204 : 500;
    }
    
    public function accept()
    {
        if (empty($this->chatMessage)) {
            throw new Exception("Too little information is known about this report to perform any action");
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');
        $result = $db->executeQuery('UPDATE report SET status = "accepted" WHERE chat_message = ? LIMIT 1', array($this->chatMessage));
        return ($result) ? 204 : 500;
    }

    public function reject()
    {
        if (empty($this->chatMessage)) {
            throw new Exception("Too little information is known about this report to perform any action");
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');
        $result = $db->executeQuery('UPDATE report SET status = "rejected" WHERE chat_message = ? LIMIT 1', array($this->chatMessage));
        return ($result) ? 204 : 500;
    }

    public function finish()
    {
        if (empty($this->chatMessage)) {
            throw new Exception("Too little information is known about this report to perform any action");
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');
        $result = $db->executeQuery('UPDATE report SET status = "fixed" WHERE chat_message = ? LIMIT 1', array($this->chatMessage));
        return ($result) ? 204 : 500;
    }

    public function delete()
    {
        if (empty($this->chatMessage)) {
            throw new Exception("Too little information is known about this report to perform any action");
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');
        $result = $db->executeQuery('DELETE FROM report WHERE chat_message = ? LIMIT 1', array($this->chatMessage));
        return ($result) ? 204 : 500;
    }
}
