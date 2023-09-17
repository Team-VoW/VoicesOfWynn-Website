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
     * @return array|null Associative array with elements "code" (the 16-character code) and "active" (boolean) or NULL,
     * if no code for the user exists
     */
    public function getCode(string $discordUserId): ?array
    {
        $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
        $result = $db->fetchQuery('SELECT code,active FROM access_codes WHERE discord_id = ? LIMIT 1;', [$discordUserId]);
        if ($result === false) {
            return null;
        }
        return $result;
    }

    /**
     * Method checking whether a premium access code is valid
     * @param string $code Access code to check
     * @return int An HTTP-like code indicator:
     * 200 if the code is valid, 402 if the code is disabled, 404 if code does not exist
     */
    public function verify(string $code): int
    {
        $code = strtoupper($code);
        $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
        $result = $db->fetchQuery('SELECT active FROM access_codes WHERE code = ? LIMIT 1;', [$code]);
        if ($result === false) {
            return 404;
        } else {
            return ($result['active']) ? 200 : 402;
        }
    }

    /**
     * Method marking a certain user's access code as disabled
     * @param string $discordUserId Discord ID of the user whose access code should be deactivated
     * @return bool TRUE on success, FALSE on failure
     */
    public function deactivate(string $discordUserId)
    {
        $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
        return $db->executeQuery('UPDATE access_codes SET active = 0 WHERE discord_id = ?;', [$discordUserId]);
    }

    /**
     * Method marking a certain user's access code as enabled
     * @param string $discordUserId Discord ID of the user whose access code should be activated
     * @return bool TRUE on success, FALSE on failure
     */
    public function activate(string $discordUserId)
    {
        $db = new Db('Api/PremiumAuthenticator/DbInfo.ini');
        return $db->executeQuery('UPDATE access_codes SET active = 1 WHERE discord_id = ?;', [$discordUserId]);
    }
}

