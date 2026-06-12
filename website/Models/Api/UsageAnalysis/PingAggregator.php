<?php

namespace VoicesOfWynn\Models\Api\UsageAnalysis;

use DateTime;
use VoicesOfWynn\Models\Db;

class PingAggregator
{

    public function aggregateUpToDate(DateTime $firstToNotProcess): int
    {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');

        $result = $db->fetchQuery('SELECT date FROM daily ORDER BY date DESC LIMIT 1');
        $latestAgreggatedDate = $result['date'];

        $currentlyProcessing = new DateTime($latestAgreggatedDate);
        $currentlyProcessing->modify('+ 1 day');
        while ($currentlyProcessing < $firstToNotProcess) {
            $result = $db->fetchQuery('SELECT COUNT(*) AS "cnt" FROM ping WHERE time LIKE ?', array($currentlyProcessing->format('Y-m-d').'%'));
            if ($result === false) {
                $bootups = 0;
            }
            else {
                $bootups = $result['cnt'];
            }

            $db->startTransaction();
            $result1 = $db->executeQuery('DELETE FROM ping WHERE time LIKE ?', array($currentlyProcessing->format('Y-m-d').'%'));
            $result2 = $db->executeQuery('INSERT INTO daily(date,bootups) VALUES (?,?)', array($currentlyProcessing->format('Y-m-d'), $bootups));
            if (!($result1 && $result2)) {
                $db->rollbackTransaction();
                return 500;
            }
            if (!$db->commitTransaction()) {
                return 500;
            }

            $currentlyProcessing->modify('+ 1 day');
        }
        return 204;
    }
}
