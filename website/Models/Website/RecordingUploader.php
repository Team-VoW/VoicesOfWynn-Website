<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;
use VoicesOfWynn\Models\Storage\Storage;

/**
 * Class taking care of processing new recording uploads
 */
class RecordingUploader
{
    private array $errors = [];

    private array $successes = [];

    /**
     * Method processing uploaded recordings.
     * For every processed file, a new element (as associative array) is saved either into the $errors property or the $successes property.
     * Use the provided getters for these properties after calling this method to retrieve the status of the operation.
     * @param array $uploadedFiles Array from $_FILES['recording'] with unchanged structure, which contains the files to process
     * @param bool $overwrite TRUE, if files with the same name should be overwritten, FALSE, if they should be renamed instead, default FALSE
     * @param int|null $overrideQuestId ID of the quest to assign new recordings to, if left as null, the program will try to decide the ID based on the filename of every single recording
     * @param int|null $overrideNpcId ID of the NPC to assign new recordings to, if left as null, the program will try to decide the ID based on the filename of every single recording
     * @return int Number of successfully uploaded recordings
     */
    public function upload(array $uploadedFiles, bool $overwrite = false, ?int $overrideQuestId = null, ?int $overrideNpcId = null): int
    {
        $recordingsCount = count($uploadedFiles['name']);
        for ($i = 0; $i < $recordingsCount; $i++) {
            $questId = $overrideQuestId;
            $npcId = $overrideNpcId;

            $filename = $uploadedFiles['name'][$i];
            $tempName = $uploadedFiles['tmp_name'][$i];
            $type = $uploadedFiles['type'][$i];
            $error = $uploadedFiles['error'][$i];

            if ($error !== UPLOAD_ERR_OK) {
                $this->errors[] = [
                    'code' => 422,
                    'msg' => 'Unprocessable Entity',
                    'desc' => 'An error occurred during the file uploading: error code '.$error,
                    'file' => $filename
                ];
                continue;
            }

            if ($type !== 'audio/ogg' && $type !== 'video/ogg' && $type !== 'application/ogg') { //Our server is treating audio/ogg as WHATEVER/ogg for some reason
                $this->errors[] = [
                    'code' => 415,
                    'msg' => 'Unsupported Media Type',
                    'desc' => 'The uploaded file is not in the correct format, only OGG files are allowed, MIME-type '.$type.' provided',
                    'file' => $filename
                ];
                continue;
            }

            $db = new Db('Website/DbInfo.ini');

            $filenameParts = explode('-', $filename);

            if (count($filenameParts) !== 3) {
                $this->errors[] = [
                    'code' => 400,
                    'msg' => 'Bad Request',
                    'desc' => 'File doesn\'t follow the required format (questname-npcname-line.ogg)',
                    'file' => $filename
                ];
                continue;
            }

            $line = explode('.', $filenameParts[2])[0];

            if (empty($questId)) {
                $degeneratedQuestName = $filenameParts[0];
                $result = $db->fetchQuery('SELECT quest_id FROM quest WHERE degenerated_name = ? LIMIT 1;', [$degeneratedQuestName]);
                if ($result === false) {
                    $this->errors[] = [
                        'code' => 404,
                        'msg' => 'Not Found',
                        'desc' => 'Quest with a name corresponding to '.$degeneratedQuestName.' couldn\'t be found',
                        'file' => $filename
                    ];
                    continue;
                }
                $questId = $result['quest_id'];
            }
            if (empty($npcId)) {
                $degeneratedNpcName = $filenameParts[1];

                $result = $db->fetchQuery(
                    'SELECT npc_id FROM npc WHERE degenerated_name = ? AND npc_id IN (
                        SELECT npc_id FROM npc_quest WHERE quest_id = ?
                    ) LIMIT 1;'
                    , [$degeneratedNpcName, $questId]);

                if ($result === false) {
                    $this->errors[] = [
                        'code' => 404,
                        'msg' => 'Not Found',
                        'desc' => 'NPC with a name corresponding to '.$degeneratedNpcName.' couldn\'t be found',
                        'file' => $filename
                    ];
                    continue;
                }
                $npcId = $result['npc_id'];
            }

            //In case a file with this name already exists, append "_([number])" to it (before the extension)
            //Increase the number for as long as files with the name exist (a bit like in Windows)
            $fileReplaced = false;
            $fileRenamed = false;
            $originalFilename = $filename;
            $storage = Storage::get();
            if ($storage->exists('recordings/'.$filename)) {
                if ($overwrite) {
                    $storage->delete('recordings/'.$filename);
                    $fileReplaced = true;
                } else {
                    $fileRenamed = true;
                    $filename = str_replace('.ogg', '_(1).ogg', $filename);
                    for ($j = 2; $storage->exists('recordings/'.$filename); $j++) {
                        $filename = preg_replace('/_\(\d*\)\.ogg$/', '_('.$j.').ogg', $filename);
                    }
                }
            }

            $storage->upload($tempName, 'recordings/'.$filename, 'audio/ogg');

            if (!$fileReplaced) {
                //Insert a new database record only if a new recording file was created on the server
                $db->executeQuery('INSERT INTO recording (npc_id,quest_id,line,file) VALUES (?,?,?,?)', array(
                    $npcId,
                    $questId,
                    $line,
                    $filename
                ));
                $this->successes[] = [
                    'code' => 201,
                    'msg' => 'Created',
                    'desc' => $fileRenamed ? 'File was uploaded and renamed to '.$filename.' in order to prevent a conflict' : 'File was uploaded',
                    'file' => $originalFilename
                ];
            } else {
                $this->successes[] = [
                    'code' => 200,
                    'msg' => 'OK',
                    'desc' => 'File was uploaded ond overwrote an existing file with the same name',
                    'file' => $originalFilename
                ];
            }
        }

        return count($this->successes);
    }

    public function getErrors(): array
    {
        return $this->errors;
    }

    public function getSuccesses(): array
    {
        return $this->successes;
    }
}

