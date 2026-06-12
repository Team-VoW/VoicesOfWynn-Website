<?php

namespace VoicesOfWynn\Controllers\Website;

use DateTime;
use Exception;
use VoicesOfWynn\Controllers\Controller;
use VoicesOfWynn\Models\Api\MessageBroadcast\BroadcastLoader;
use VoicesOfWynn\Models\Website\Comment;
use VoicesOfWynn\Models\Website\DiscordRole;
use VoicesOfWynn\Models\Website\ModDownload;
use VoicesOfWynn\Models\Website\Npc;
use VoicesOfWynn\Models\Website\Quest;
use VoicesOfWynn\Models\Website\Recording;
use VoicesOfWynn\Models\Website\User;

/**
 * Base class for all controllers displaying a webpage
 * Also contains methods for rendering the final website, that are being called from the Router
 */
abstract class WebpageController extends Controller
{

    private const VIEWS_FOLDER = 'Views'; //In case of change, make sure to update a value in ErrorController.php too

    /**
     * @var $data array Data obtained by all controllers in the process
     */
    protected static array $data = array();

    /**
     * @var $views array List of views to use, from the most outer one to the most inner one
     */
    protected static array $views = array('base');

    /**
     * @var $cssFiles array List of CSS files to include into the final webpage; all CSS files must be in the 'css'
     *     folder
     */
    protected static array $cssFiles = array('base');

    /**
     * @var $jsFiles array List of JS files to include into the final webpage; all JS files must be in the 'js' folder
     */
    protected static array $jsFiles = array('jquery');

    /**
     * Controller constructor setting data for the base view and setting the Content-Type header
     * Since specific controllers don't have a constructor, this will be invoked every time a new constructor is
     * instantiated
     */
    public function __construct()
    {
        header('Content-Type: text/html; charset=UTF-8');
        self::$data['base_broadcast'] = (new BroadcastLoader())->loadBroadcast();
    }

    /**
     * @inheritDoc
     */
    public abstract function process(array $args): int;

    /**
     * Method composing the final website from the list of views and data supplied by specific controllers
     * This is called from the RouterController
     * @return string Final website to send to the user
     */
    public function displayView(): string
    {
        //Sanitize against XSS attack
        $sanitized = array();
        foreach (self::$data as $key => $value) {
            $sanitized[$key] = $this->sanitize($value);
        }
        extract($sanitized);

        ob_start();
        require $this->getNextView();
        $result = ob_get_contents();
        ob_end_clean();
        return $result;
    }

    /**
     * Method sanitizing one variable of type int, double, string or array against XSS attack
     * @param $value mixed Variable to sanitize
     * @return mixed Sanitized variable of the same type
     */
    private function sanitize($value)
    {
        $return = null;
        switch (gettype($value)) {
            case 'NULL':
                $return = null;
                break;
            case 'boolean':
                $return = (bool)$value;
                break;
            case 'string':
            case 'double':
            case 'integer':
                $return = htmlspecialchars($value, ENT_QUOTES);
                break;
            case 'array':
                $return = array();
                foreach ($value as $key => $val) {
                    $return[$key] = $this->sanitize($val);
                }
                break;
            case 'object':
                if ($value instanceof DateTime) {
                    //According to https://stackoverflow.com/a/64624314/14011077, DateTime is safe in terms of XSS
                    $return = $value;
                }
                else if ($value instanceof DiscordRole) {
                    $return = new DiscordRole("TempName");
                    $return->name = $this->sanitize($value->name);
                    $return->color = $this->sanitize($value->color);
                    $return->weight = $this->sanitize($value->weight);
                }
                else if ($value instanceof User) {
                    $id = $this->sanitize($value->getId());
                    $discordId = $this->sanitize($value->getDiscordId());
                    $email = $this->sanitize($value->getEmail());
                    $name = $this->sanitize($value->getName());
                    $avatarLink = $this->sanitize($value->getAvatar());
                    $bio = $value->getBio(); //Don't sanitize, dangerous tags are removed before saving to the database
                    $lore = $this->sanitize($value->getLore());
                    $roles = $this->sanitize($value->getRoles());

                    $value->setData(array(
                        'id' => $id,
                        'discordId' => $discordId,
                        'email' => $email,
                        'displayName' => $name,
                        'avatarLink' => $avatarLink,
                        'bio' => $bio,
                        'lore' => $lore
                    ));
                    $value->setRoles($roles);

                    $return = $value;
                }
                else if ($value instanceof Quest) {
                    $attributes = [];
                    $attributes['id'] = $this->sanitize($value->getId());
                    $attributes['name'] = $this->sanitize($value->getName());
                    $attributes['degenerated_name'] = $this->sanitize($value->getDegeneratedName());
                    if ($value->getScriptAuthor() !== null) {
                        $attributes['writer'] = $this->sanitize($value->getScriptAuthor());
                    }
                    $quest = new Quest($attributes);
                    foreach ($value->getNpcs() ?? [] as $npc) {
                        $quest->addNpc($this->sanitize($npc));
                    }
                    $return = $quest;
                }
                else if ($value instanceof Npc) {
                    $attr = array();
                    $attr['id'] = $this->sanitize($value->getId());
                    $attr['name'] = $this->sanitize($value->getName());
                    $attr['archived'] = $this->sanitize($value->isArchived());
                    $attr['recordings_count'] = $this->sanitize($value->getRecordingsCount());
                    $attr['upvotes'] = $this->sanitize($value->getUpvotes());
                    $attr['downvotes'] = $this->sanitize($value->getDownvotes());
                    $attr['comments'] = $this->sanitize($value->getCommentsCount());
                    $voiceActor = ($value->getVoiceActor() !== null) ? $this->sanitize($value->getVoiceActor()) : null;
                    $npc = new Npc($attr);
                    if ($voiceActor !== null) {
                        $npc->setVoiceActor($voiceActor);
                    }
                    if ($value->getSoundEditor() !== null) {
                        foreach ($value->getSoundEditor() as $quest => $editor) {
                            $npc->setSoundEditor(new Quest(['id' => $this->sanitize($quest)]), $this->sanitize($editor));
                        }
                    }
                    foreach ($value->getRecordings() as $recording) {
                        $npc->addRecording($this->sanitize($recording));
                    }
                    $return = $npc;
                }
                else if ($value instanceof Recording) {
                    $attr = array();
                    $attr['id'] = $this->sanitize($value->id);
                    $attr['npc_id'] = $this->sanitize($value->npcId);
                    $attr['quest_id'] = $this->sanitize($value->questId);
                    $attr['line'] = $this->sanitize($value->line);
                    $attr['file'] = $this->sanitize($value->file);
                    $attr['upvotes'] = $this->sanitize($value->upvotes);
                    $attr['downvotes'] = $this->sanitize($value->downvotes);
                    $attr['comments'] = $this->sanitize($value->comments);
                    $attr['archived'] = $this->sanitize($value->archived);
                    $return = new Recording($attr);
                }
                else if ($value instanceof Comment) {
                    $attr = array();
                    $attr['id'] = $this->sanitize($value->id);
                    $attr['verified'] = $this->sanitize($value->verified);
                    $attr['userId'] = $this->sanitize($value->userId);
                    $attr['ip'] = $this->sanitize($value->ip);
                    $attr['name'] = $this->sanitize($value->name);
                    $attr['email'] = $this->sanitize($value->email);
                    $attr['content'] = nl2br($this->sanitize($value->content));
                    $attr['recording_id'] = $this->sanitize($value->recordingId);
                    $attr['gravatar'] = $this->sanitize($value->gravatar);
                    $return = new Comment($attr);
                }
                else if ($value instanceof ModDownload) {
                    $attr = array();
                    $attr['id'] = $this->sanitize($value->id);
                    $attr['releaseType'] = $this->sanitize($value->releaseType);
                    $attr['mcVersion'] = $this->sanitize($value->mcVersion);
                    $attr['wynnVersion'] = $this->sanitize($value->wynnVersion);
                    $attr['version'] = $this->sanitize($value->version);
                    $attr['changelog'] = $value->changelog; //Don't sanitize, dangerous tags are removed before saving to the database
                    $attr['releaseDate'] = $this->sanitize($value->releaseDate);
                    $attr['fileName'] = $this->sanitize($value->fileName);
                    $attr['size'] = $this->sanitize($value->size);
                    $attr['downloadedTimes'] = $this->sanitize($value->downloadedTimes);
                    $return = new ModDownload($attr);
                }
                else {
                    throw new Exception('Object variable of class '.get_class($value).' couldn\'t be sanitized');
                }
                break;
            default:
                throw new Exception('Variable of type '.gettype($value).' couldn\'t be sanitized');
        }
        return $return;
    }

    /**
     * Method returning a path to the next (more inner) view to display
     * @return string Path to the view
     */
    public function getNextView(): string
    {
        return self::VIEWS_FOLDER.'/'.array_shift(self::$views).'.phtml';
    }

    /**
     * Method finding and returning a variable to be inserted to a view by its name
     * @param $variableName
     * @return mixed
     */
    public static function ins($variableName)
    {
        echo __FILE__;
        $viewName = basename(__FILE__, '.phtml');
        $viewName = str_replace('-', '', $viewName);
        return ${$viewName.'_'.$variableName};
    }

    /**
     * Method loading the Minecraft account UUID from $_REQUEST['uuid'], stripping it off dashes, saving it to $_SESSION['uuid'] and returning it.
     * If $_REQUEST['uuid'] is empty, value from $_SESSION['uuid'] is returned.
     * This is useful for controllers of all webpages that contain the NPC voting feature (upvote/downvote).
     * @return string|null UUID of the client (can be invalid or forged) or NULL if it isn't neither provided in $_REQUEST or saved in $_SESSION
     */
    protected static function loadUUID() : ?string
    {
        $uuid = (empty($_REQUEST['uuid'])) ? null : str_replace('-', '', $_REQUEST['uuid']); //Rewrite UUID in session
        if (is_null($uuid) && isset($_SESSION['uuid'])) {
            //Refresh UUID from session
            $uuid = $_SESSION['uuid'];
        }
        $_SESSION['uuid'] = $uuid;
        return $uuid;
    }
}

