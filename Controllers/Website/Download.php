<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Website\DownloadsManager;
use VoicesOfWynn\Models\Website\ModDownload;

class Download extends WebpageController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch ($args[0]) {
            case 'view':
                self::$data['base_title'] = 'Download Voices of Wynn';
                self::$data['base_description'] = 'Download your desired version of the mod quickly and easily.';
                self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Download';

                self::$views[] = "download-details";
                self::$cssFiles[] = "download-details";
                self::$jsFiles[] = "download-details";

                $result = (new Db('Website/DbInfo.ini'))->fetchQuery(
                    'SELECT * FROM download WHERE download_id = ? LIMIT 1;',
                    array($args[1])
                );
                if ($result === false) {
                    return 404;
                }
                $downloadObject = new ModDownload($result);
                self::$data['downloaddetails_download'] = $downloadObject;

                break;
            case 'get':
                $downloadId = (int)$args[1];
                $downloadManager = new DownloadsManager();
                $result = $downloadManager->downloadFile($downloadId);
                if ($result) {
                    exit;
                }
                else {
                    return 404;
                }
        }

        return true;
    }
}