<?php

namespace VoicesOfWynn\Controllers\Errors;

use VoicesOfWynn\Controllers\Controller;

/**
 * Base class for all error controllers
 * These controllers are always either displaying a simple static error webpage, or just set an HTTP response code
 * Also contains methods for rendering the final website, that are being called from the Router
 */
abstract class ErrorController extends Controller
{

    private const VIEWS_FOLDER = 'Views'; //In case of change, make sure to update a value in WebpageController.php too

    /**
     * @var $data array Data obtained by all controllers in the process
     */
    protected static array $data = array();

    /**
     * @var $view string Name of the view containing the error webpage
     */
    protected static string $view;

    /**
     * @var $cssFiles array List of names of CSS files applied to the error webpage
     */
    protected static array $cssFiles = array("errors");

    /**
     * Method capable of setting the view and headers for the error page
     * @param array $args An array containing one boolean element. If the element's value is true, an error webpage is
     * set up, otherwise, only the error code is set in the headers.
     * @return int 1 (or TRUE), if everything worked
     */
    public function process(array $args): int
    {
        if ($args[0]) {
            $this->displayErrorWebsite();
        }
        $this->sendHttpErrorHeader();
        return true;
    }

    /**
     * Method setting up data for displaying the error webpage
     */
    protected abstract function displayErrorWebsite();

    /**
     * Method sending the HTTP error code in a header
     */
    protected abstract function sendHttpErrorHeader();

    /**
     * Method displaying the error webpage, if data for it has been set in a specific error controller
     * This is called from the RouterController
     * @return string Final website to send to the user or empty string, if only HTTP error code should be sent
     */
    public function getResult(): string
    {
        //No need for sanitization against XSS attack for a static webpage
        extract(self::$data);

        ob_start();
        require self::VIEWS_FOLDER.'/'.self::$view.'.phtml';
        $result = ob_get_contents();
        ob_end_clean();
        return $result;
    }
}

