<?php

namespace VoicesOfWynn\Models\Api\LineReporting;

use VoicesOfWynn\Models\Db;

class ReportManager
{

    /**
     * Processing method for the request used to update the status of a single report or to delete it
     * @param array $chatMessages Array of chat messages whose reports should be affected
     * @param string $action "y" for accepting, "n" for rejecting, "v" for marking as voiced and "r" for deleting
     * @return int HTTP response code
     */
    public function updateReport(array $chatMessages, string $action): int
    {
        $db = new Db('Api/LineReporting/DbInfo.ini');

        if (count($chatMessages) === 1) {
            $reportInfo = $db->fetchQuery('SELECT * FROM report WHERE chat_message = ? LIMIT 1', array($chatMessages[0]));
        
            if (empty($reportInfo)) {
                return 404;
            }
            $report = new Report();
            $report->load($reportInfo);
    
            switch ($action) {
                case 'r':
                    return $report->delete();
                case 'y':
                    return $report->accept();
                case 'n':
                    return $report->reject();
                case 'v':
                    return $report->finish();
                default:
                    return 400;
            }
        } else {
            $db = new Db('Api/LineReporting/DbInfo.ini');
            $inString = implode(',', array_fill(0, count($chatMessages), '?'));
            switch ($action) {
                case 'r':
                    $query = 'DELETE FROM report WHERE chat_message IN ('.$inString.')';
                    $parameters = $chatMessages;
                    break;
                case 'y':
                    $query = 'UPDATE report SET status = ? WHERE chat_message IN ('.$inString.');'
                    $parameters = array_merge(['accepted'], $chatMessages);
                    break;
                case 'n':
                    $query = 'UPDATE report SET status = ? WHERE chat_message IN ('.$inString.');'
                    $parameters = array_merge(['rejected'], $chatMessages);
                    break;
                case 'v':
                    $query = 'UPDATE report SET status = ? WHERE chat_message IN ('.$inString.');'
                    $parameters = array_merge(['fixed'], $chatMessages);
                    break;
                default:
                    return 400;
            }
            $result = $db->executeQuery($query, $parameters);
            return ($result) ? 204 : 500;
        }
    }

    /**
     * Processing method for a PUT request used to reset the status of all unresolved reports
     * If a NPC name is sent with the request under name "npc", only reports of such NPC are reset
     * @param string|null $npcName Name of the NPC, whose line's reports should be reset (optional)
     * @return int HTTP response code
     */
    public function resetForwardedReports(string $npcName = null){
        $db = new Db('Api/LineReporting/DbInfo.ini');

        if (!empty($npcName)) {
            $query = 'UPDATE report SET status = "unprocessed" WHERE npc_name = ? AND status = "forwarded"';
            $parameters = array($npcName);
        }
        else {
            $query = 'UPDATE report SET status = "unprocessed" WHERE status = "forwarded"';
            $parameters = array();
        }

        return ($db->executeQuery($query, $parameters))? 204 : 500;
    }
}
