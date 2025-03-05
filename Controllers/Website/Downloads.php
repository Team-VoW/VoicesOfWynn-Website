<?php

namespace VoicesOfWynn\Controllers\Website;

use VoicesOfWynn\Models\Website\DownloadsManager;

header('Location: https://modrinth.com/mod/vow');
exit();

class Downloads extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch (array_shift($args)) {
            case 'forge':
                $downloadsManager = new DownloadsManager();
                self::$data['downloadslist_versions'] = $downloadsManager->listDownloads(DownloadsManager::FORGE_VERSIONS);
                self::$data['downloadslist_platform'] = 'Forge';
                self::$views[] = 'downloads-list';
                self::$cssFiles[] = 'downloads-list';
                break;
            case 'fabric':
                $downloadsManager = new DownloadsManager();
                self::$data['downloadslist_versions'] = $downloadsManager->listDownloads(DownloadsManager::FABRIC_VERSIONS);
                self::$data['downloadslist_platform'] = 'Fabric';
                self::$views[] = 'downloads-list';
                self::$cssFiles[] = 'downloads-list';
                break;
        }

        self::$data['base_title'] = 'Downloads';
        self::$data['base_description'] = 'Downloads of all versions of the mod are available here. Download the latest version, install it to your Minecraft client and enjoy voiced Wynncraft.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Downloads,Download';

        return true;
    }
}