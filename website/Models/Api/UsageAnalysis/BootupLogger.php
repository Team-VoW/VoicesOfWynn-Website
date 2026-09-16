<?php

namespace VoicesOfWynn\Models\Api\UsageAnalysis;

use DateTime;
use VoicesOfWynn\Models\Db;

class BootupLogger
{
    public const MINIMUM_DELAY_BETWEEN_PINGS_BY_IP = 3600; //Seconds that must pass between pings from one IP to be recorded
    public const MINIMUM_DELAY_BETWEEN_PINGS_BY_UUID = 86400; //Seconds that must pass between pings from one player to be recorded

    /**
     * Logs a mod bootup to the usage analysis database
     * @param string $playerUUID Hashed player UUID (will be saved in the database like this)
     * @param string $playerIp Hashed client IP (will be saved in the database like this)
     * @return int HTTP response code
     * @throws \Exception
     */
    public function logBootup(string $playerUUID, string $playerIp) {

        if (!($this->verifySpamByIp($playerIp) && $this->verifySpamByUUID($playerUUID))) {
            //Spam ping
            return 204;
        }

        $result1 = $this->logDailyStats($playerUUID, $playerIp); //true in case of success
        $result2 = $this->logAllTimeStars($playerUUID);
        return ($result1 && $result2) ? 200 : 500;
    }

    private function verifySpamByIp($hashedIp): bool {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        $result = $db->fetchQuery('SELECT time FROM ping WHERE ip = ? ORDER BY time DESC LIMIT 1;', array($hashedIp));
        if ($result && time() - (new DateTime($result['time']))->getTimestamp() < self::MINIMUM_DELAY_BETWEEN_PINGS_BY_IP) {
            return false;
        }
        return true;
    }

    private function verifySpamByUUID($hashedUUID): bool {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        $result = $db->fetchQuery('SELECT time FROM ping WHERE uuid = ? ORDER BY time DESC LIMIT 1;', array($hashedUUID));
        if ($result && time() - (new DateTime($result['time']))->getTimestamp() < self::MINIMUM_DELAY_BETWEEN_PINGS_BY_UUID) {
            return false;
        }
        return true;
    }

    private function logDailyStats($hashedUUID, $hashedIP): bool
    {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        return $db->executeQuery('INSERT INTO ping(uuid,ip,time) VALUES (?,?,?)', array($hashedUUID, $hashedIP,
            (new DateTime('now'))->format('Y-m-d H:i:s')));
    }

    private function logAllTimeStars($hashedUUID): bool
    {
        $db = new Db('Api/UsageAnalysis/DbInfo.ini');
        //This used to compare $e->getCode() against MySQL's 1062, but PDO reports the SQLSTATE
        //'23000' instead, so every returning player fell through to the failure branch and got a
        //500 back - which the mod's HttpURLConnection turns into an exception, silently costing
        //them the version check, the kill switch and the broadcasts. INSERT IGNORE removes the
        //need to inspect the error at all.
        return $db->executeQuery('INSERT IGNORE INTO total(uuid) VALUES (?)', array($hashedUUID));
    }
}
