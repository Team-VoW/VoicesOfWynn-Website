<?php

namespace VoicesOfWynn\Models\Api\PremiumAuthenticator;


use Exception;
use PDOException;
use VoicesOfWynn\Models\Db;

class PremiumCodeManager
{

    /**
     * Method generating new premium access code for a single Discord User
     * @param string $discordUserId Discord user ID of the user to which the new code should belong (numerical
     * string or BIGINT in MySQL)
     * @return bool The generated access code, if the new code was generated and saved successfully, FALSE otherwise
     */
    public function createNew(string $discordUserId)
    {
        try {
            $code = strtoupper(substr(base_convert(bin2hex(random_bytes(11)), 16, 36), 0, 16));
        } catch (Exception $e) {
            return false;
        }

        try {
            $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
            $result = $db->executeQuery('INSERT INTO access_codes(code,discord_id) VALUES (?,?);', [$code, $discordUserId]);
        } catch (PDOException $ex) {
            return false;
        }

        return $code;
    }

    /**
     * Method loading an premium access code for the given Discord user
     * @param string $discordUserId Discord user ID of the user whose code should be loaded (numerical string or BIGINT
     * in MySQL)
     * @return string|null The code (16 characters) or NULL, if no code for the user exists
     */
    public function getCode(string $discordUserId): ?string
    {
        $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
        $result = $db->fetchQuery('SELECT code FROM access_codes WHERE discord_id = ? LIMIT 1;', [$discordUserId]);
        if ($result === false) {
            return null;
        }
        return $result['code'];
    }

    /**
     * Method checking whether an premium access code is valid
     * @param string $code Access code to check
     * @return bool TRUE if the code is valid, FALSE if not
     */
    public function verify(string $code): bool
    {
        $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
        $result = $db->fetchQuery('SELECT COUNT(*) AS "cnt" FROM access_codes WHERE code = ? LIMIT 1;', [$code]);
        return (bool)$result["cnt"];
    }
}

