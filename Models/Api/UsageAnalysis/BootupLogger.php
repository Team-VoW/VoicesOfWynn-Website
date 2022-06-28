<?php

namespace VoicesOfWynn\Models\Api\UsageAnalysis;

use VoicesOfWynn\Models\Db;

class BootupLogger
{
    private const MINIMUM_DELAY_BETWEEN_PINGS_BY_IP = 3600; //Seconds that must pass between pings from one IP to be recorded
    private const MINIMUM_DELAY_BETWEEN_PINGS_BY_UUID = 86400; //Seconds that must pass between pings from one player to be recorded

    public function logBootup(string $playerUUID, string $playerIp) {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');

        if (!($this->verifySpamByIp($playerIp) && $this->verifySpamByUUID($playerUUID))) {
            //Spam ping
            return 204;
        }

        //Hash the values
        $playerUUID = hash('sha256', $playerUUID);
        $playerIp = hash('sha256', $playerIp);

        $result = $db->executeQuery('INSERT INTO ping(uuid,ip,time) VALUES (?,?,?)', array($playerUUID, $playerIp, time()));
        return ($result) ? 201 : 500;
    }

    private function verifySpamByIp($ip): bool {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        $ip = hash('sha256', $ip);
        $result = $db->fetchQuery('SELECT time FROM ping WHERE ip = ? ORDER BY time DESC LIMIT 1;', array($ip));
        if ($result && time() - $result['time'] < self::MINIMUM_DELAY_BETWEEN_PINGS_BY_IP) {
            return false;
        }
        return true;
    }

    private function verifySpamByUUID($uuid): bool {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        $uuid = hash('sha256', $uuid);
        $result = $db->fetchQuery('SELECT time FROM ping WHERE uuid = ? ORDER BY time DESC LIMIT 1;', array($uuid));
        if ($result && time() - $result['time'] < self::MINIMUM_DELAY_BETWEEN_PINGS_BY_UUID) {
            return false;
        }
        return true;
    }
}
