<?php

namespace VoicesOfWynn\Models\Website;

use DateTime;
use VoicesOfWynn\Controllers\Website\Download;

class ModDownload
{
    private const RELEASE_TYPE_ALPHA = 'alpha';
    private const RELEASE_TYPE_BETA = 'beta';
    private const RELEASE_TYPE_PRERELEASE = 'pre-release';
    private const RELEASE_TYPE_RELEASE = 'release';
    private const RELEASE_TYPE_PATCH = 'patch';

    private const MINECRAFT_VERSION_REGEX = '/^\d\.\d\d?(\.\d\d?)?$/';
    private const WYNNCRAFT_VERSION_REGEX = '/^\d\.\d\d?(\.\d\d?)?$/';
    private const MOD_VERSION_REGEX = '/^\d\d?\.\d\d?\d?(\.\d\d?\d?)?$/';

    private const CHANGELOG_MAX_LENGTH = 65535; //In bytes / ASCII characters
    const MODFILE_MAX_SIZE = 4294967295; //In bytes

    public int $id;
    public string $releaseType;
    public string $mcVersion;
    public string $wynnVersion;
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
                case 'wynnVersion':
                case 'wynn_version':
                case 'wynncraftVersion':
                case 'wynncraft_version':
                    $this->wynnVersion = $value;
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

    /**
     * Method validating user-inputted data for the current object.
     * The following attributes are validated:
     * - releaseType
     * - mcVersion
     * - wynnVersion
     * - version
     * - changelog
     * - fileName
     * The following attributes might also be changed
     * - changelog
     * - fileName
     * - size (is filled by the file size of the file specified in the fileName attribute)
     * @return bool True if the whole validation process passed successfully
     * @throws UserException In case of an invalid value
     */
    public function validate(): bool
    {
        //Validate that all fields were filled
        if (
            empty($this->releaseType) ||
            empty($this->mcVersion) ||
            empty($this->wynnVersion) ||
            empty($this->version) ||
            empty($this->changelog) ||
            empty($this->fileName)
        ) {
            throw new UserException('All fields must be filled to create a new release');
        }

            //Validate the type
            if (!in_array($this->releaseType, array(
                self::RELEASE_TYPE_ALPHA,
                self::RELEASE_TYPE_BETA,
                self::RELEASE_TYPE_PRERELEASE,
                self::RELEASE_TYPE_RELEASE,
                self::RELEASE_TYPE_PATCH
            ))) {
                throw new UserException('Invalid release type');
            }

        //Validate Minecraft version
        if (!preg_match(self::MINECRAFT_VERSION_REGEX, $this->mcVersion)) {
            throw new UserException('Invalid format of Minecraft version');
        }

        //Validate Wynncraft version
        if (!preg_match(self::WYNNCRAFT_VERSION_REGEX, $this->wynnVersion)) {
            throw new UserException('Invalid format of Wynncraft version');
        }

        //Validate mod version
        if (!preg_match(self::MOD_VERSION_REGEX, $this->mcVersion)) {
            throw new UserException('Invalid format of mod version');
        }

        //Validate changelog
        if (strlen($this->changelog) > self::CHANGELOG_MAX_LENGTH) {
            throw new UserException('Changelog is too long');
        }
        $adv = new AccountDataValidator();
        $this->changelog = $adv->sanitizeBio($this->changelog); //Purify the HTML for consistent database

        //Validate file name
        if (!file_exists(DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.$this->fileName)) {
            throw new UserException('Mod file of this name wasn\'t found on the server. Make sure to first log in into website administration and upload the .jar file to /files/mod');
        }
        //Rename the file in case it doesn't follow the naming convention
        $idealFileName = str_replace('.', '_', $this->mcVersion).
            '-'.
            str_replace('.', '_', $this->version).
            '.jar';
        if ($this->fileName !== $idealFileName) {
            if (file_exists(DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.$idealFileName)) {
                throw new UserException('It looks like the .jar file for this version of the mod is already uploaded. Try changing either the Minecraft version or the Mod version field.');
            }
            rename(
                DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.$this->fileName,
                DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.$idealFileName
            );
            $this->fileName = $idealFileName;
        }

        //Validate size
        if (filesize(DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.$this->fileName) > self::MODFILE_MAX_SIZE) {
            throw new UserException('The mod file is too big, its size cannot be saved in the database. Contact shady_medic on Discord to make him increase the maximum value.');
        }
        $this->size = filesize(DownloadsManager::ROOT_DOWNLOADS_DIRECTORY.'/'.$this->fileName);

        return true;
    }

}

