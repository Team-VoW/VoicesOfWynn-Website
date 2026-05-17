<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\RecordingUploader;

class Upload extends WebpageController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
            default:
                return 405;
        }
    }

    /**
     * Method processing GET requests to the Upload webpage (the form should be displayed)
     * @param array $args
     * @return int
     */
    private function get(array $args): int
    {
        self::$data['base_description'] = 'Tool for the administrators to mass-upload new recordings to the website.';

        if (!isset(self::$data['upload_uploadErrors'])) {
            self::$data['upload_uploadErrors'] = array();
        }

        if (!isset(self::$data['upload_uploadSuccesses'])) {
            self::$data['upload_uploadSuccesses'] = array();
        }

        self::$views[] = 'upload';
        return true;
    }

    /**
     * Method processing POST requests to the Upload webpage (the form was just submitted)
     * @param array $args
     * @return int
     */
    private function post(array $args): int
    {
        $uploader = new RecordingUploader();
        $questId = (empty($_POST['questId']) ? null : $_POST['questId']);
        $npcId = (empty($_POST['npcId']) ? null : $_POST['npcId']);
        $overwriteFiles = isset($_POST['overwrite']) && $_POST['overwrite'] === 'on';
        $uploader->upload($_FILES['recordings'], $overwriteFiles, $questId, $npcId);
        self::$data['upload_uploadErrors'] = $uploader->getErrors();
        self::$data['upload_uploadSuccesses'] = $uploader->getSuccesses();

        return $this->get($args);
    }
}

