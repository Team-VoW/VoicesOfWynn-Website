<?php

namespace VoicesOfWynn\Models\Website;

use DateTime;

class ModDownload
{
    public int $id;
    public string $releaseType;
    public string $mcVersion;
    public string $version;
    public string $changelog;
    public DateTime $releaseDate;
    public string $fileName;
    public int $size;
    public int $downloadedTimes;

    /**
     * @throws \Exception
     */
    public function __construct(array $data)
    {
        foreach ($data as $key => $value) {
            switch ($key) {
                case 'id':
                case 'download_id':
                    $this->id = $value;
                    break;
                case 'releaseType':
                case 'release_type':
                case 'type':
                    $this->releaseType = $value;
                    break;
                case 'mcVersion':
                case 'mc_version':
                case 'minecraftVersion':
                case 'minecraft_version':
                    $this->mcVersion = $value;
                    break;
                case 'version':
                    $this->version = $value;
                    break;
                case 'description':
                case 'desc':
                case 'changelog':
                    $this->changelog = $value;
                    break;
                case 'releaseDate':
                case 'release_date':
                case 'released':
                case 'release_on':
                case 'date':
                    if (gettype($value) === 'object') {
                        $this->releaseDate = $value;
                    } else {
                        $this->releaseDate = new DateTime($value);
                    }
                    break;
                case 'file':
                case 'filename':
                case 'fileName':
                case 'file_name':
                    $this->fileName = $value;
                    break;
                case 'size':
                case 'bytes':
                    $this->size = $value;
                    break;
                case 'downloadedTimes':
                case 'downloaded_times':
                case 'downloads':
                case 'downloaded':
                    $this->downloadedTimes = $value;
                    break;
            }
        }
    }
}

