<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class DownloadsManager
{
    public const ROOT_DOWNLOADS_DIRECTORY = 'files/mod';
    private const FILE_NAME_FORMATS = 'VoicesOfWynn-MC{mcVersion}-v{version}.jar';

    /**
     * Sends the mod file to the client to download and increases the count of downloads for that file
     * It also sets all headers for the file download request
     * @param int $downloadId ID of the JAR file to download; the download count will be incremented for the download with this ID
     * @return bool false, if the download is not found, nothing otherwise (script is terminated with exit();
     * @throws \InvalidArgumentException
     * @throws \Exception
     */
    public function downloadFile(int $downloadId): bool
    {
        $db = new Db('Website/DbInfo.ini');
        
        $result = $db->fetchQuery('SELECT download_link,filename,mc_version,version,size FROM download WHERE download_id = ?', array($downloadId));
        if ($result === false) {
            return false;
        }

        $filePath = empty($result['download_link']) ? self::ROOT_DOWNLOADS_DIRECTORY.'/'.$result['filename'] : $result['download_link'];
        $fileName = str_replace('{mcVersion}', $result['mc_version'],
            str_replace('{version}', $result['version'], self::FILE_NAME_FORMATS)
        );
        $fileSize = $result['size'];

        if (!empty($result['download_link'])) {
            header('Location: '.$result['download_link']); //Remote download
        } else {
            $obContent = ob_get_contents(); //Pause the output bufferer to prevent memory overflow caused by "readfile()"
            ob_end_clean();

            header('Content-Description: File Transfer');
            header('Content-Type: application/java-archive');
            header('Content-Disposition: attachment; filename="'.$fileName.'"');
            header('Expires: 0');
            header('Cache-Control: must-revalidate');
            header('Pragma: public');
            header('Content-Length: '.$fileSize);
            readfile($filePath);

            ob_start(); //Resume the output bufferer
            echo $obContent;
        }
        return $db->executeQuery('UPDATE download SET downloaded_times = downloaded_times + 1 WHERE download_id = ?', array($downloadId));
    }

}

