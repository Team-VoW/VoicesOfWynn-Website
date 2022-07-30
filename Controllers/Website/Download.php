<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Website\DownloadsManager;
use VoicesOfWynn\Controllers\Controller;

class Download extends Controller
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch ($args[0]) {
            case 'view':
                //TODO render a release details webpage
                break;
            case 'get':
                $downloadId = $args[0];
                $downloadManager = new DownloadsManager();
                $downloadManager->downloadFile($downloadId);
                break;
        }

        return true;
    }
}