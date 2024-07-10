<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\DownloadsManager;
use VoicesOfWynn\Models\Website\UserException;

class NewRelease extends WebpageController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
            default:
                return 405;
        }
    }

    private function get(array $args): int
    {
        self::$data['base_description'] = 'Tool for the administrators to create new releases and downloads.';

        self::$data['newrelease_type'] = '';
        self::$data['newrelease_version'] = '';
        self::$data['newrelease_wynn_version'] = '';
        self::$data['newrelease_mc_version'] = '';
        self::$data['newrelease_filename'] = '';
        self::$data['newrelease_changelog'] = '';
        self::$data['newrelease_error'] = '';
        self::$data['newrelease_releaseId'] = '';

        self::$views[] = 'new-release';
        self::$jsFiles[] = 'tinymce_changelog';
        return true;
    }

    private function post(array $args): int
    {
        $result = $this->get(array());

        $downloadManager = new DownloadsManager();
        try {
            $releaseId = $downloadManager->createDownload(
                $_POST['type'],
                $_POST['mcVersion'],
                $_POST['wynnVersion'],
                $_POST['version'],
                $_POST['changelog'],
                $_POST['filename']
            );
            self::$data['newrelease_releaseId'] = $releaseId;

        } catch (UserException $e) {
            self::$data['newrelease_type'] = $_POST['type'];
            self::$data['newrelease_version'] = $_POST['version'];
            self::$data['newrelease_wynn_version'] = $_POST['wynnVersion'];
            self::$data['newrelease_mc_version'] = $_POST['mcVersion'];
            self::$data['newrelease_filename'] = $_POST['filename'];
            self::$data['newrelease_changelog'] = $_POST['changelog'];
            self::$data['newrelease_error'] = $e->getMessage();
        }

        return $result;
    }
}