<?php

namespace VoicesOfWynn\Models;

class VersionManager
{
    public function getVersions(): array
    {
        //All files in /files/mod should be named "voices_of_wynn_[version]", for example "voices_of_wynn_7.3"
        $versions = array();
        $folder = dir('files/mod');
        while (($version = $folder->read()) !== false) {
            if ($version[0] === '.' || $version === 'latest.jar') {
                continue;
            }
            
            $versionItem = array(
                'link' => '/files/mod/'.$version,
                'name' => substr($version, 15, strlen($version) - (15 + 4)) //15 is the length of "voices_of_wynn_", 4 is for ".jar"
            );
            $versions[] = $versionItem;
        }
        return array_reverse($versions);
    }
}

