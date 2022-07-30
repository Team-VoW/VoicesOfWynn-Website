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
        $downloadId = $args[0];
        $downloadManager = new DownloadsManager();
        $downloadManager->downloadFile($downloadId);

        return true;
    }
}