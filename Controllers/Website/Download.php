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
                self::$cssFiles[] = 'article-css-reset';
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
                self::$data['downloaddetails_installerSize'] = filesize(DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.DownloadsManager::INSTALLER_FILE_NAME);

                break;
            case 'get':
                $downloadId = (int)$args[1];
                $downloadType = ($args[2] === 'installer') ? DownloadsManager::DOWNLOAD_TYPE_INSTALLER : DownloadsManager::DOWNLOAD_TYPE_MODFILE;
                $downloadManager = new DownloadsManager();
                $result = $downloadManager->downloadFile($downloadType, $downloadId);
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