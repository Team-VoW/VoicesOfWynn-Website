<?php

namespace VoicesOfWynn\Models\Api\MessageBroadcast;

use DateTime;
use DateTimeZone;
use PDO;
use PDOException;
use VoicesOfWynn\Models\Db;

class BroadcastLoader
{
    /**
     * Loads and returns all messages that should currently be broadcast, as an array of strings
     * (or an empty array if nothing should be broadcast).
     * Broadcasts are stored in the api database and edited from the staff Admin page; the windows
     * are kept in UTC, which is also how the .NET API reads them.
     * @return array Array of string messages to broadcast
     */
    public function loadBroadcast(): array
    {
        $now = (new DateTime('now', new DateTimeZone('UTC')))->format('Y-m-d H:i:s');

        try {
            $result = (new Db('Api/MessageBroadcast/DbInfo.ini'))->fetchQuery(
                'SELECT content FROM mod_broadcast WHERE active_from <= ? AND active_until >= ? ORDER BY active_from, broadcast_id',
                array($now, $now),
                true,
                PDO::FETCH_ASSOC
            );
        } catch (PDOException $e) {
            error_log('Loading broadcasts failed: '.$e->getMessage());
            return array();
        }

        if (!$result) {
            return array();
        }

        return array_column($result, 'content');
    }
}
