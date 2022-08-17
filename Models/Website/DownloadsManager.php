<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class DownloadsManager
{
    public const ROOT_DOWNLOADS_DIRECTORY = 'files/mod';
    private const FILE_NAME_FORMATS = 'VoicesOfWynn-MC{mcVersion}-v{version}.jar';

    /**
     * Lists all downloads, newest to oldest
     * @return ModDownload[] Array of ModDownloads objects
     * @throws \Exception
     */
    public function listDownloads(): array
    {
        $db = new Db('Website/DbInfo.ini');
        $result = $db->fetchQuery('SELECT * FROM download ORDER BY release_date DESC', array(), true);

        if ($result === false) {
            return array();
        }

        $downloads = array();
        foreach ($result as $downloadData) {
            $downloads[] = new ModDownload($downloadData);
        }

        return $downloads;
    }

    /**
     * Sends the mod file to the client to download and increases the count of downloads for that file
     * It also sets all headers for the file download request
     * @param int $downloadId
     * @return bool false, if the download is not found, nothing otherwise (script is terminated with exit();
     * @throws \Exception
     */
    public function downloadFile(int $downloadId): bool
    {
        $db = new Db('Website/DbInfo.ini');
        $result = $db->fetchQuery('SELECT filename,mc_version,version,size FROM download WHERE download_id = ?', array($downloadId));
        if ($result === false) {
            return false;
        }

        $filePath = self::ROOT_DOWNLOADS_DIRECTORY.'/'.$result['filename'];
        $fileName = str_replace('{mcVersion}', $result['mc_version'],
            str_replace('{version}', $result['version'], self::FILE_NAME_FORMATS)
        );
        $fileSize = $result['size'];

        header('Content-Description: File Transfer');
        header('Content-Type: application/java-archive');
        header('Content-Disposition: attachment; filename="'.$fileName.'"');
        header('Expires: 0');
        header('Cache-Control: must-revalidate');
        header('Pragma: public');
        header('Content-Length: '.$fileSize);
        readfile($filePath);

        return $db->executeQuery('UPDATE download SET downloaded_times = downloaded_times + 1 WHERE download_id = ?', array($downloadId));
    }

    /**
     * @param string $type Type of the release, must be one of the constants of the ModDownload class
     * @param string $mcVersion Version of Minecraft for which this download is made
     * @param string $wynnVersion Version of Wynncraft for which this download is made
     * @param string $version Version of the mod
     * @param string $changelog HTML text containing the changelog for the new version
     * @param string $filename Name of the file on the server. NOTE: The .jar file must be uploaded into the /files/mod directory when this function is run
     * @return int ID of the new release if the download has successfully been created
     * @throws UserException In case one or more of the provided strings is invalid
     */
    public function createDownload(string $type, string $mcVersion, string $wynnVersion, string $version, string $changelog, string $filename): int
    {
        $download = new ModDownload(array(
            'type' => $type,
            'mcVersion' => $mcVersion,
            'wynnVersion' => $wynnVersion,
            'version' => $version,
            'changelog' => $changelog,
            'filename' => $filename,
        ));

        $download->validate();

        $db = new Db('Website/DbInfo.ini');
        return $db->executeQuery('INSERT INTO download(release_type,mc_version,wynn_version,version,changelog,filename,size) VALUES (?,?,?,?,?,?,?)', array(
            $download->releaseType,
            $download->mcVersion,
            $download->wynnVersion,
            $download->version,
            $download->changelog,
            $download->fileName,
            $download->size
        ), true);
    }
}

