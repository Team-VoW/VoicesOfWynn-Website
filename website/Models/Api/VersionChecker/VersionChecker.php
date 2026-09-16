<?php

namespace VoicesOfWynn\Models\Api\VersionChecker;

use PDOException;
use VoicesOfWynn\Models\Db;

class VersionChecker
{

    /**
     * Loads the mod release info and returns it under the key names the shipped mod clients read.
     * The values themselves live in the api database (mod_release) and are edited from the staff
     * Admin page, which is also what the .NET /mod/bootup endpoint serves - so this route and the
     * new one can never disagree about which version is current.
     * @return array Array of values keyed the way the mod client expects them
     */
    public function getLatestVersionInfo(): array
    {
        try {
            $release = (new Db('Api/VersionChecker/DbInfo.ini'))->fetchQuery(
                'SELECT latest_version, update_notification_version, kill_switch_version, download_url,
                        changelog_url, audio_base_url, audio_mirror_urls
                 FROM mod_release WHERE id = 1'
            );
        } catch (PDOException $e) {
            error_log('Loading the mod release info failed: '.$e->getMessage());
            return array();
        }

        if (!$release) {
            return array();
        }

        $mirrors = json_decode($release['audio_mirror_urls'], true);

        return array(
            'fabric_newestVersion' => $release['latest_version'],
            'fabric_updateNotification' => $release['update_notification_version'],
            'fabric_killSwitchVersion' => $release['kill_switch_version'],
            'fabric_directUpdateLink' => $release['download_url'],
            'fabric_updateInfopageLink' => $release['changelog_url'],
            'azure_blob_link' => $release['audio_base_url'],
            'audio_urls' => is_array($mirrors) ? $mirrors : array(),
        );
    }
}
