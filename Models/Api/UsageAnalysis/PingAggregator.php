<?php

namespace VoicesOfWynn\Models\Api\UsageAnalysis;

use DateTime;
use VoicesOfWynn\Models\Db;

class PingAggregator
{

    public function aggregateDay(DateTime $latestToProcess): int
    {
        $startTimestamp = $latestToProcess->getTimestamp() - 86400;
        $startDate = (new DateTime('@'.$startTimestamp))->format('Y-m-d H:i:s');
        $endDate = $latestToProcess->format('Y-m-d H:i:s');

        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        $result = $db->fetchQuery('SELECT COUNT(*) AS "cnt" FROM ping WHERE time BETWEEN ? AND ?', array($startDate, $endDate));
        if ($result === false) {
            $bootups = 0;
        }
        else {
            $bootups = $result['cnt'];
        }

        $db->startTransaction();
        $result1 = $db->executeQuery('DELETE FROM ping WHERE time BETWEEN ? AND ? LIMIT ?', array($startDate, $endDate, $bootups));
        $result2 = $db->executeQuery('INSERT INTO daily(date,bootups) VALUES (?,?)', array((new DateTime('@'.($startTimestamp + 43200)))->format('Y-m-d'), $bootups));
        if (!($result1 && $result2)) {
            $db->rollbackTransaction();
            return 500;
        }
        $result3 = $db->commitTransaction();

        return ($result3) ? 204 : 500;
    }
}
