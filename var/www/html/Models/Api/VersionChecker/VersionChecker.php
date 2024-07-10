<?php

namespace VoicesOfWynn\Models\Api\VersionChecker;

class VersionChecker
{

    private const CONFIG_FILE = 'VersionConfig/version.ini';

    /**
     * Loads and returns the version info from an INI config file
     * @return array Array of values loaded by parse_ini_file()
     */
    public function getLatestVersionInfo() : array
    {
        return parse_ini_file(self::CONFIG_FILE);
    }
}
