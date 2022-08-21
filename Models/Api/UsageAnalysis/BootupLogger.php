<?php

namespace VoicesOfWynn\Models\Api\UsageAnalysis;

use DateTime;
use PDOException;
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
        $result2 = $this->logAllTimeStars($playerUUID); //true or 204 in case of success, both evaluate to TRUE in the condition below
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
        try {
            $result = $db->executeQuery('INSERT INTO total(uuid) VALUES (?)', array($hashedUUID));
        } catch (PDOException $e) {
            if ($e->getCode() === 1062) { //1062 = error code for duplicated entry in a column required unique values
                return 204; //Request OK, but nothing new was saved
            }
            else {
                return false; //Query failed because of who knows why
            }
        }
        return $result; //New UUID hash saved
    }
}
