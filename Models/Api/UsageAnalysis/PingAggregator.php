<?php

namespace VoicesOfWynn\Models\Api\UsageAnalysis;

use DateTime;
use VoicesOfWynn\Models\Db;

class PingAggregator
{

    public function aggregateDay(DateTime $latestToProcess): int
    {
        $startTimestamp = $latestToProcess->getTimestamp() - 86400;
        $endTimestamp = $latestToProcess->getTimestamp();

        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        $result = $db->fetchQuery('SELECT COUNT(*) AS "cnt" FROM ping WHERE time BETWEEN ? AND ?', array($startTimestamp, $endTimestamp));
        if ($result === false) {
            $bootups = 0;
        }
        else {
            $bootups = $result['cnt'];
        }

        $db->startTransaction();
        $result1 = $db->executeQuery('DELETE FROM ping WHERE time BETWEEN ? AND ? LIMIT ?', array($startTimestamp, $endTimestamp, $bootups));
        $result2 = $db->executeQuery('INSERT INTO daily(date,bootups) VALUES (?,?)', array((new DateTime($startTimestamp + 43200))->format('Y-M-d')));
        $result3 = $db->commitTransaction();

        return ($result1 && $result2 && $result3) ? 204 : 500;
    }
}
