<?php

namespace VoicesOfWynn\Models\Api\LineReporting;

use PDO;
use PDOException;
use VoicesOfWynn\Models\Db;

class ReportReader
{

    /**
     * @var array|Report A storage for results of the methods fetching report info from the dataabase, so they can return error codes instead
     */
    public $result;

    /**
     * Processing method for a request used to list all reports
     * It also sets status of all forwarded lines as "Forwarded" and won't return them again
     * @param string|null $npcName Name of the NPC, whose lines should be returned
     * @return int HTTP response code
     */
    public function listUnvoicedLineReports(string $npcName = null): int
    {
        if (!empty($npcName)) {
            $query1 = 'SELECT npc_name,pos_x,pos_y,pos_z,player,chat_message FROM report WHERE status = "unprocessed" AND npc_name = ?';
            $query2 = 'UPDATE report SET status = "forwarded" WHERE status = "unprocessed" AND npc_name = ?';
            $parameters = array($npcName);
        }
        else {
            $query1 = 'SELECT npc_name,pos_x,pos_y,pos_z,player,chat_message FROM report WHERE status = "unprocessed"';
            $query2 = 'UPDATE report SET status = "forwarded" WHERE status = "unprocessed"';
            $parameters = array();
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');

        try {
            $resultSelect = $db->fetchQuery($query1, $parameters, true, PDO::FETCH_ASSOC);
            $resultUpdate = $db->executeQuery($query2, $parameters);
        } catch (PDOException $e) {
            return 500;
        }

        if ($resultSelect === false) {
            //No unprocessed reports
            $resultSelect = array();
        }
        $this->result = array();
        foreach ($resultSelect as $reportInfo) {
            $report = new Report();
            $report->load($reportInfo);
            $this->result[] = $report;
        }

        return ($resultUpdate) ? 200 : 500;
    }

    /**
     * Processing method for a request used to get all information about a single report
     * Partial search is performed and if more lines are returned, only the first one is outputted
     * @param string $line A line (or it's part) whose report should be returned from the database
     * @return int HTTP response code
     * @throws \Exception
     */
    public function getRawReportData(string $line): int
    {
        if (empty($line)) {
            return 406;
        }

        $db = new Db('Api/LineReporting/DbInfo.ini');

        try {
            $result = $db->fetchQuery('SELECT * FROM report WHERE chat_message LIKE ? LIMIT 1', array('%'.$line.'%'), false, PDO::FETCH_ASSOC);
        } catch (PDOException $e) {;
            return 500;
        }

        if ($result === false) {
            //No such report
            return 404;
        }

        $report = new Report();
        $report->load($result);
        $this->result = $report;

        return 200;
    }

    /**
     * Processing method for a GET request used to list all records of a certain status
     * @param string|null $npcName Name of the NPC, whose lines should be returned
     * @return int HTTP response code
     */
    public function getReportsByNpc(string $npcName = null, array $statuses): int
    {
        $inString = '('.rtrim(str_repeat('?,', count($statuses)), ',').')';
        if (!empty($npcName)) {
            $npcName = $_GET['npc'];
            $query = 'SELECT npc_name,pos_x,pos_y,pos_z,chat_message FROM report WHERE status IN '.$inString.' AND npc_name = ?;';
            array_push($statuses, $npcName);
        }
        else {
            $query = 'SELECT npc_name,pos_x,pos_y,pos_z,chat_message FROM report WHERE status IN '.$inString.';';
        }
        $parameters = $statuses;

        $db = new Db('Api/LineReporting/DbInfo.ini');

        try {
            $result = $db->fetchQuery($query, $parameters, true, PDO::FETCH_ASSOC);
        } catch (PDOException $e) {
            return 500;
        }

        $this->result = array();
        foreach ($result as $reportInfo) {
            $report = new Report();
            $report->load($reportInfo);
            $this->result[] = $report;
        }

        return 200;
    }
}
